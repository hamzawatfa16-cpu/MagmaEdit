using MagmaEdit.Integration;

namespace MagmaEdit.McpServer;

/// <summary>Targets the running desktop editor for the authenticated MagmaEdit user and session.</summary>
public sealed class MagmaEditAutomationTarget : IAsyncDisposable
{
    private readonly LiveEditorPipeClient _liveClient;
    private readonly AutomationClientContext _client;
    private readonly MagmaEditUserContext _userContext;
    private readonly string? _projectPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private EditorAutomationSession? _fileSession;
    private bool _disposed;

    public MagmaEditAutomationTarget(
        string? projectPath,
        AutomationClientContext client,
        MagmaEditUserContext userContext)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            _projectPath = Path.GetFullPath(projectPath);
        }

        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(userContext);
        _client = client;
        _userContext = userContext;
        _liveClient = new LiveEditorPipeClient();
    }

    public async Task<EditorCommandResult> ExecuteAsync(
        EditorCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (string userId, string? sessionId) = GetIdentity();
            LiveEditorPipeResponse? liveResponse = await TrySendLiveAsync(
                new LiveEditorPipeRequest(
                    LiveEditorPipeProtocol.ExecuteOperation,
                    request,
                    UserId: userId,
                    SessionId: sessionId),
                cancellationToken).ConfigureAwait(false);

            if (liveResponse is not null)
            {
                return liveResponse.CommandResult ?? new EditorCommandResult(
                    false,
                    "The live MagmaEdit editor returned no command result.");
            }

            return GetFileSession().Execute(request);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<EditorProjectState> GetStateAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (string userId, string? sessionId) = GetIdentity();
            LiveEditorPipeResponse? liveResponse = await TrySendLiveAsync(
                new LiveEditorPipeRequest(
                    LiveEditorPipeProtocol.GetStateOperation,
                    UserId: userId,
                    SessionId: sessionId),
                cancellationToken).ConfigureAwait(false);

            if (liveResponse is not null)
            {
                return liveResponse.State ?? throw new InvalidDataException(
                    "The live MagmaEdit editor returned no project state.");
            }

            return GetFileSession().GetState();
        }
        finally
        {
            _gate.Release();
        }
    }

    private (string UserId, string? SessionId) GetIdentity()
    {
        string userId = string.IsNullOrWhiteSpace(_userContext.UserId) ? _client.ClientId : _userContext.UserId;
        return (userId, _userContext.SessionId);
    }

    private async Task<LiveEditorPipeResponse?> TrySendLiveAsync(
        LiveEditorPipeRequest request,
        CancellationToken cancellationToken)
    {
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(TimeSpan.FromMilliseconds(350));

        try
        {
            return await _liveClient.SendAsync(request, connectTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private EditorAutomationSession GetFileSession()
    {
        if (_fileSession is not null)
        {
            return _fileSession;
        }

        if (string.IsNullOrWhiteSpace(_projectPath))
        {
            throw new InvalidOperationException(
                "No live MagmaEdit desktop session is available and no project path was configured.");
        }

        _fileSession = EditorAutomationSession.Load(_projectPath, _client);
        return _fileSession;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
