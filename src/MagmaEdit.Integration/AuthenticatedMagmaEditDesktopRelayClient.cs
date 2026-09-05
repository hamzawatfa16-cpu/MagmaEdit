using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MagmaEdit.Integration;

/// <summary>Maintains the authenticated outbound desktop WebSocket used by the hosted broker relay.</summary>
public sealed class AuthenticatedMagmaEditDesktopRelayClient : IAsyncDisposable
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
    private readonly Uri _brokerBaseUri;
    private readonly IMagmaEditBrokerCredentialProvider _credentialProvider;
    private readonly Func<LiveEditorPipeRequest, CancellationToken, Task<LiveEditorPipeResponse>> _requestHandler;
    private readonly TimeSpan _retryDelay;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _runTask;
    private int _disposed;

    public AuthenticatedMagmaEditDesktopRelayClient(
        Uri brokerBaseUri,
        IMagmaEditBrokerCredentialProvider credentialProvider,
        Func<LiveEditorPipeRequest, CancellationToken, Task<LiveEditorPipeResponse>> requestHandler,
        TimeSpan? retryDelay = null)
    {
        ArgumentNullException.ThrowIfNull(brokerBaseUri);
        if (!string.Equals(brokerBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The broker base URI must use HTTPS.", nameof(brokerBaseUri));
        }

        _brokerBaseUri = brokerBaseUri;
        _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        _requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
        _retryDelay = retryDelay ?? DefaultRetryDelay;
        if (_retryDelay <= TimeSpan.Zero || _retryDelay > MaxRetryDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay), "The relay retry delay must be greater than zero and no longer than thirty seconds.");
        }
    }

    public Task RunAsync(
        Func<MagmaEditSessionDescriptor?> currentSessionProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentSessionProvider);
        return RunCoreAsync(currentSessionProvider, cancellationToken);
    }

    private async Task RunCoreAsync(
        Func<MagmaEditSessionDescriptor?> currentSessionProvider,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedShutdown = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdown.Token);
        CancellationToken token = linkedShutdown.Token;
        TimeSpan delay = _retryDelay;
        string? connectedSessionId = null;

        while (!token.IsCancellationRequested)
        {
            MagmaEditSessionDescriptor? session = currentSessionProvider();
            if (session is null)
            {
                connectedSessionId = null;
                await DelayAsync(delay, token).ConfigureAwait(false);
                continue;
            }

            if (!string.Equals(connectedSessionId, session.SessionId, StringComparison.Ordinal))
            {
                delay = _retryDelay;
            }

            try
            {
                connectedSessionId = session.SessionId;
                await RunConnectionAsync(session, token).ConfigureAwait(false);
                delay = _retryDelay;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is WebSocketException or IOException or InvalidDataException or HttpRequestException)
            {
                StartupDiagnostics.WriteComponentFailure("desktop broker relay", exception);
                await DelayAsync(delay, token).ConfigureAwait(false);
                delay = TimeSpan.FromTicks(Math.Min(MaxRetryDelay.Ticks, delay.Ticks * 2));
            }
        }
    }

    private async Task RunConnectionAsync(
        MagmaEditSessionDescriptor session,
        CancellationToken cancellationToken)
    {
        MagmaEditBrokerCredential credential = await _credentialProvider
            .GetCredentialAsync(cancellationToken)
            .ConfigureAwait(false);

        Uri endpoint = BuildWebSocketUri(session.UserId, session.SessionId);
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", $"Bearer {credential.AccessToken}");
        socket.Options.SetRequestHeader("X-MagmaEdit-Request-Id", Guid.NewGuid().ToString("N"));
        socket.Options.SetRequestHeader(
            "X-MagmaEdit-Timestamp",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

        await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        await ReceiveLoopAsync(socket, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        using var message = new MemoryStream();

        while (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidDataException("The desktop broker relay only accepts text messages.");
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                if (message.Length > 4 * 1024 * 1024)
                {
                    throw new InvalidDataException("The desktop broker relay request exceeded the maximum message size.");
                }

                continue;
            }

            RelayRequestEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<RelayRequestEnvelope>(
                    message.GetBuffer().AsSpan(0, checked((int)message.Length)),
                    LiveEditorPipeProtocol.JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The broker returned invalid relay JSON.", exception);
            }
            finally
            {
                message.SetLength(0);
            }

            if (envelope is null || string.IsNullOrWhiteSpace(envelope.CorrelationId) || envelope.Request is null)
            {
                continue;
            }

            LiveEditorPipeResponse response;
            try
            {
                response = await _requestHandler(envelope.Request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                response = new LiveEditorPipeResponse(false, exception.Message);
            }

            string json = JsonSerializer.Serialize(
                new RelayResponseEnvelope(envelope.CorrelationId, response),
                LiveEditorPipeProtocol.JsonOptions);
            byte[] payload = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
    }

    private Uri BuildWebSocketUri(string userId, string sessionId)
    {
        var builder = new UriBuilder(_brokerBaseUri)
        {
            Scheme = "wss",
            Path = CombinePath(_brokerBaseUri.AbsolutePath, "v1/desktop-sessions/connect"),
            Query = $"userId={Uri.EscapeDataString(userId)}&sessionId={Uri.EscapeDataString(sessionId)}"
        };
        return builder.Uri;
    }

    private static string CombinePath(string basePath, string childPath)
    {
        string normalizedBase = basePath.TrimEnd('/');
        return $"{normalizedBase}/{childPath.TrimStart('/')}";
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _shutdown.Cancel();
            _shutdown.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private sealed record RelayRequestEnvelope(string CorrelationId, LiveEditorPipeRequest Request);

    private sealed record RelayResponseEnvelope(string CorrelationId, LiveEditorPipeResponse Response);
}
