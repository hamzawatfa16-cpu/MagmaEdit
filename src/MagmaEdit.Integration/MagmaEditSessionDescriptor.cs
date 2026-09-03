namespace MagmaEdit.Integration;

/// <summary>Describes one authenticated desktop editor session registered with a session broker.</summary>
public sealed record MagmaEditSessionDescriptor(
    string UserId,
    string SessionId,
    string ConnectionId,
    string Endpoint,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> Capabilities);

/// <summary>Request used when a desktop editor registers a session with a broker.</summary>
public sealed record MagmaEditSessionRegistration(
    string UserId,
    string SessionId,
    string ConnectionId,
    string Endpoint,
    TimeSpan LeaseDuration,
    IReadOnlyList<string> Capabilities);
