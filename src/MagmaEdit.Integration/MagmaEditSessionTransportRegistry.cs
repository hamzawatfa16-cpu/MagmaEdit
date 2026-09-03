namespace MagmaEdit.Integration;

/// <summary>
/// Development transport router backed by the authenticated desktop-session broker.
/// Production hosts should replace the connection registry with durable shared infrastructure.
/// </summary>
public sealed class MagmaEditSessionTransportRegistry : IMagmaEditSessionTransport
{
    private readonly MagmaEditSessionBroker _broker;
    private readonly object _sync = new();
    private readonly Dictionary<string, IMagmaEditDesktopSessionConnection> _connections = new(StringComparer.Ordinal);

    public MagmaEditSessionTransportRegistry(MagmaEditSessionBroker broker)
    {
        ArgumentNullException.ThrowIfNull(broker);
        _broker = broker;
    }

    public void Attach(IMagmaEditDesktopSessionConnection connection, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!_broker.TryGet(connection.UserId, now, out MagmaEditSessionDescriptor? session)
            || session is null
            || !string.Equals(session.SessionId, connection.SessionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The desktop session is not registered or has expired.");
        }

        lock (_sync)
        {
            if (_connections.ContainsKey(connection.SessionId))
            {
                throw new InvalidOperationException("A desktop connection is already attached to this session.");
            }

            _connections.Add(connection.SessionId, connection);
        }
    }

    public bool Detach(string userId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        string normalizedUserId = userId.Trim();
        string normalizedSessionId = sessionId.Trim();

        lock (_sync)
        {
            if (!_connections.TryGetValue(normalizedSessionId, out IMagmaEditDesktopSessionConnection? connection))
            {
                return false;
            }

            if (!string.Equals(connection.UserId, normalizedUserId, StringComparison.Ordinal))
            {
                return false;
            }

            return _connections.Remove(normalizedSessionId);
        }
    }

    public async Task<MagmaEditSessionTransportResponse> SendAsync(
        MagmaEditSessionTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);

        IMagmaEditDesktopSessionConnection? connection;
        lock (_sync)
        {
            _connections.TryGetValue(request.SessionId.Trim(), out connection);
        }

        if (connection is null
            || !string.Equals(connection.UserId, request.UserId.Trim(), StringComparison.Ordinal)
            || !string.Equals(connection.SessionId, request.SessionId.Trim(), StringComparison.Ordinal))
        {
            return new MagmaEditSessionTransportResponse(
                false,
                "The requested desktop session is unavailable or is not authorized for this user.",
                CorrelationId: request.CorrelationId);
        }

        return await connection.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
