using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MagmaEdit.Integration;

namespace MagmaEdit.Broker;

/// <summary>Routes one authenticated broker request to the exact connected desktop session.</summary>
public sealed class MagmaEditDesktopRelayHub
{
    private readonly ConcurrentDictionary<string, Connection> _connections = new(StringComparer.Ordinal);
    private readonly TimeSpan _responseTimeout;

    public MagmaEditDesktopRelayHub(TimeSpan? responseTimeout = null)
    {
        _responseTimeout = responseTimeout ?? TimeSpan.FromSeconds(30);
        if (_responseTimeout <= TimeSpan.Zero || _responseTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(responseTimeout), "Relay response timeout must be greater than zero and no longer than five minutes.");
        }
    }

    public async Task RunDesktopConnectionAsync(
        string userId,
        string sessionId,
        WebSocket socket,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(socket);

        string key = CreateKey(userId, sessionId);
        var connection = new Connection(socket);
        if (!_connections.TryAdd(key, connection))
        {
            await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Desktop session is already connected.").ConfigureAwait(false);
            return;
        }

        try
        {
            await ReceiveLoopAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connections.TryRemove(new KeyValuePair<string, Connection>(key, connection));
            connection.FailPending(new IOException("The desktop relay connection closed."));
            await CloseAsync(socket, WebSocketCloseStatus.NormalClosure, "Connection closed.").ConfigureAwait(false);
            connection.Dispose();
        }
    }

    public async Task<LiveEditorPipeResponse> RelayAsync(
        string userId,
        string sessionId,
        LiveEditorPipeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(request);

        if (!_connections.TryGetValue(CreateKey(userId, sessionId), out Connection? connection))
        {
            throw new InvalidOperationException("The requested MagmaEdit desktop session is not connected to the broker.");
        }

        return await connection.SendAsync(request, _responseTimeout, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReceiveLoopAsync(Connection connection, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (connection.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            ValueWebSocketReceiveResult result = await connection.Socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            RelayResponseEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<RelayResponseEnvelope>(message.GetBuffer().AsSpan(0, checked((int)message.Length)), LiveEditorPipeProtocol.JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The desktop relay returned invalid JSON.", exception);
            }
            finally
            {
                message.SetLength(0);
            }

            if (envelope is null || string.IsNullOrWhiteSpace(envelope.CorrelationId))
            {
                continue;
            }

            connection.Complete(envelope.CorrelationId, envelope.Response);
        }
    }

    private static string CreateKey(string userId, string sessionId) =>
        $"{userId.Trim()}\n{sessionId.Trim()}";

    private static async Task CloseAsync(WebSocket socket, WebSocketCloseStatus status, string description)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseAsync(status, description, CancellationToken.None).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
            }
        }
    }

    private sealed class Connection : IDisposable
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<LiveEditorPipeResponse>> _pending = new(StringComparer.Ordinal);
        private int _disposed;

        public Connection(WebSocket socket) => Socket = socket;

        public WebSocket Socket { get; }

        public async Task<LiveEditorPipeResponse> SendAsync(
            LiveEditorPipeRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            string correlationId = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<LiveEditorPipeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(correlationId, completion))
            {
                throw new InvalidOperationException("Could not allocate a relay correlation ID.");
            }

            try
            {
                string json = JsonSerializer.Serialize(new RelayRequestEnvelope(correlationId, request), LiveEditorPipeProtocol.JsonOptions);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(timeout);
                await _sendGate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    await Socket.SendAsync(payload, WebSocketMessageType.Text, true, linked.Token).ConfigureAwait(false);
                }
                finally
                {
                    _sendGate.Release();
                }

                return await completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("The MagmaEdit desktop relay did not respond before the timeout.");
            }
            finally
            {
                _pending.TryRemove(correlationId, out _);
            }
        }

        public void Complete(string correlationId, LiveEditorPipeResponse response)
        {
            if (_pending.TryGetValue(correlationId, out TaskCompletionSource<LiveEditorPipeResponse>? completion))
            {
                completion.TrySetResult(response);
            }
        }

        public void FailPending(Exception exception)
        {
            foreach (TaskCompletionSource<LiveEditorPipeResponse> completion in _pending.Values)
            {
                completion.TrySetException(exception);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _sendGate.Dispose();
            }
        }
    }

    private sealed record RelayRequestEnvelope(string CorrelationId, LiveEditorPipeRequest Request);

    private sealed record RelayResponseEnvelope(string CorrelationId, LiveEditorPipeResponse Response);
}
