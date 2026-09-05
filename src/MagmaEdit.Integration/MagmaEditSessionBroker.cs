namespace MagmaEdit.Integration;

/// <summary>
/// Coordinates authenticated desktop session leases through a pluggable persistence boundary.
/// The default store is process-local for development; hosted deployments should inject durable storage.
/// </summary>
public sealed class MagmaEditSessionBroker
{
    private readonly IMagmaEditSessionStore _store;

    public MagmaEditSessionBroker(IMagmaEditSessionStore? store = null)
    {
        _store = store ?? new InMemoryMagmaEditSessionStore();
    }

    public MagmaEditSessionDescriptor Register(MagmaEditSessionRegistration registration, DateTimeOffset now) =>
        _store.Register(registration, now);

    public bool TryGet(string userId, DateTimeOffset now, out MagmaEditSessionDescriptor? descriptor) =>
        _store.TryGet(userId, now, out descriptor);

    public bool TryRenew(
        string userId,
        string sessionId,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        out MagmaEditSessionDescriptor? renewed) =>
        _store.TryRenew(userId, sessionId, leaseDuration, now, out renewed);

    public bool Unregister(string userId, string sessionId) =>
        _store.Unregister(userId, sessionId);
}
