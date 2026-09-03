using System.IO.Pipes;
using System.Text.Json;

namespace MagmaEdit.Integration;

/// <summary>Connects a local automation process to the currently running MagmaEdit desktop session.</summary>
public sealed class LiveEditorPipeClient
{
    private readonly string _pipeName;

    public LiveEditorPipeClient(string pipeName = LiveEditorPipeProtocol.PipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;
    }

    public bool IsAvailable(int timeoutMilliseconds = 250)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMilliseconds);

        using var client = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            client.Connect(timeoutMilliseconds);
            return client.IsConnected;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public async Task<LiveEditorPipeResponse> SendAsync(
        LiveEditorPipeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var client = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        await using var writer = new StreamWriter(client, leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(client, leaveOpen: true);

        string json = JsonSerializer.Serialize(request, LiveEditorPipeProtocol.JsonOptions);
        await writer.WriteLineAsync(json).ConfigureAwait(false);

        string? responseJson = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw new IOException("The MagmaEdit live editor pipe returned an empty response.");
        }

        LiveEditorPipeResponse? response =
            JsonSerializer.Deserialize<LiveEditorPipeResponse>(
                responseJson,
                LiveEditorPipeProtocol.JsonOptions);
        return response ?? throw new InvalidDataException(
            "The MagmaEdit live editor pipe returned an invalid response.");
    }
}
