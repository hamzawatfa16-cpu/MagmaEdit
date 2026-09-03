namespace MagmaEdit.Integration;

/// <summary>Stable request envelope for routing an authenticated AI/editor operation to one desktop session.</summary>
public sealed record MagmaEditSessionTransportRequest(
    string UserId,
    string SessionId,
    string Operation,
    string Payload,
    string CorrelationId);

/// <summary>Stable response envelope returned by a desktop session transport.</summary>
public sealed record MagmaEditSessionTransportResponse(
    bool Succeeded,
    string Message,
    string? Payload = null,
    string? CorrelationId = null);

/// <summary>
/// Vendor-neutral transport boundary between a hosted service and one authenticated desktop session.
/// Implementations must authenticate the session before delivering a request.
/// </summary>
public interface IMagmaEditSessionTransport
{
    Task<MagmaEditSessionTransportResponse> SendAsync(
        MagmaEditSessionTransportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents the authenticated connection owned by a registered desktop session.</summary>
public interface IMagmaEditDesktopSessionConnection
{
    string UserId { get; }

    string SessionId { get; }

    Task<MagmaEditSessionTransportResponse> SendAsync(
        MagmaEditSessionTransportRequest request,
        CancellationToken cancellationToken = default);
}
