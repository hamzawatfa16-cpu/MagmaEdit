namespace MagmaEdit.Integration;

/// <summary>Persistence boundary for authenticated desktop session leases.</summary>
public interface IMagmaEditSessionStore
{
    MagmaEditSessionDescriptor Register(
        MagmaEditSessionRegistration registration,
        DateTimeOffset now);

    bool TryGet(
        string userId,
        DateTimeOffset now,
        out MagmaEditSessionDescriptor? descriptor);

    bool TryRenew(
        string userId,
        string sessionId,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        out MagmaEditSessionDescriptor? renewed);

    bool Unregister(string userId, string sessionId);
}
