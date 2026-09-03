using System.IO.Pipes;
using System.Text.Json;
using Avalonia.Threading;
using MagmaEdit.Integration;

namespace MagmaEdit.App;

/// <summary>Hosts the local IPC endpoint used by the MCP process to reach the live desktop session.</summary>
internal sealed class LiveEditorPipeServer : IAsyncDisposable
{
    private readonly MainWindow _window;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _runTask;
    private bool _disposed;

    public LiveEditorPipeServer(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _runTask = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(
                    LiveEditorPipeProtocol.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                await HandleConnectionAsync(server, _shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteComponentFailure("live editor pipe", exception);
        }
    }

    private async Task HandleConnectionAsync(
        NamedPipeServerStream server,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(server, leaveOpen: true);
        await using var writer = new StreamWriter(server, leaveOpen: true)
        {
            AutoFlush = true
        };

        string? requestJson = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        LiveEditorPipeResponse response;

        if (string.IsNullOrWhiteSpace(requestJson))
        {
            response = Failure("The live editor pipe received an empty request.");
        }
        else
        {
            response = await ProcessRequestAsync(requestJson, cancellationToken).ConfigureAwait(false);
        }

        string json = JsonSerializer.Serialize(response, LiveEditorPipeProtocol.JsonOptions);
        await writer.WriteLineAsync(json).ConfigureAwait(false);
    }

    private async Task<LiveEditorPipeResponse> ProcessRequestAsync(
        string requestJson,
        CancellationToken cancellationToken)
    {
        try
        {
            LiveEditorPipeRequest? request = JsonSerializer.Deserialize<LiveEditorPipeRequest>(
                requestJson,
                LiveEditorPipeProtocol.JsonOptions);
            if (request is null)
            {
                return Failure("The live editor pipe request was invalid.");
            }

            if (!string.Equals(
                    request.ProtocolVersion,
                    LiveEditorPipeProtocol.Version,
                    StringComparison.Ordinal))
            {
                return Failure($"Unsupported live editor pipe protocol version '{request.ProtocolVersion}'.");
            }

            return request.Operation switch
            {
                LiveEditorPipeProtocol.ExecuteOperation => await ExecuteOnUiThreadAsync(request, cancellationToken).ConfigureAwait(false),
                LiveEditorPipeProtocol.GetStateOperation => await GetStateOnUiThreadAsync(cancellationToken).ConfigureAwait(false),
                _ => Failure($"Unsupported live editor pipe operation '{request.Operation}'.")
            };
        }
        catch (JsonException exception)
        {
            return Failure($"Invalid JSON request: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            return Failure(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Failure(exception.Message);
        }
        catch (IOException exception)
        {
            return Failure(exception.Message);
        }
    }

    private Task<LiveEditorPipeResponse> ExecuteOnUiThreadAsync(
        LiveEditorPipeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Command is null)
        {
            return Task.FromResult(Failure("An editor command is required."));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            AutomationClientContext client = CreateClient();
            var session = new LiveEditorAutomationSession(
                _window.GetProjectForExport(),
                client,
                _window.SaveProjectForExport);
            EditorCommandResult result = session.Execute(request.Command);

            if (result.Succeeded)
            {
                LiveEditorPipeUiRefresh.Refresh(
                    _window,
                    $"Project updated by AI: {result.Message}");
            }

            return new LiveEditorPipeResponse(
                result.Succeeded,
                result.Message,
                CommandResult: result);
        }, cancellationToken);
    }

    private Task<LiveEditorPipeResponse> GetStateOnUiThreadAsync(CancellationToken cancellationToken) =>
        InvokeOnUiThreadAsync(() =>
        {
            var session = new LiveEditorAutomationSession(
                _window.GetProjectForExport(),
                CreateClient(),
                _window.SaveProjectForExport);
            EditorProjectState state = session.GetState();
            return new LiveEditorPipeResponse(
                true,
                "Live editor state retrieved.",
                State: state);
        }, cancellationToken);

    private static AutomationClientContext CreateClient() => new(
        "live-mcp",
        AutomationClientKind.Mcp,
        new HashSet<EditorCommandCapability>
        {
            EditorCommandCapability.TimelineEditing,
            EditorCommandCapability.MediaManagement,
            EditorCommandCapability.History
        });

    private static Task<T> InvokeOnUiThreadAsync<T>(
        Func<T> callback,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Dispatcher.UIThread.InvokeAsync(callback).GetTask();
    }

    private static LiveEditorPipeResponse Failure(string message) =>
        new(false, message);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _shutdown.CancelAsync().ConfigureAwait(false);
        try
        {
            await _runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _shutdown.Dispose();
        }
    }
}
