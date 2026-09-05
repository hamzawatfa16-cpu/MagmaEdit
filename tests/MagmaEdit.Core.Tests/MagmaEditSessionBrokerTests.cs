using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class MagmaEditSessionBrokerTests
{
    [Fact]
    public void RegisterAndResolveReturnsActiveSessionForSameUser()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MagmaEditSessionRegistration registration = CreateRegistration("user-a", "session-a");

        MagmaEditSessionDescriptor registered = broker.Register(registration, now);

        Assert.Equal("user-a", registered.UserId);
        Assert.Equal("session-a", registered.SessionId);
        Assert.True(broker.TryGet("user-a", now.AddMinutes(1), out MagmaEditSessionDescriptor? resolved));
        Assert.Equal(registered, resolved);
    }

    [Fact]
    public void RegisterSameSessionAndConnectionRefreshesLease()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MagmaEditSessionRegistration registration = CreateRegistration("user-a", "session-a", TimeSpan.FromMinutes(5));
        MagmaEditSessionDescriptor first = broker.Register(registration, now);

        MagmaEditSessionDescriptor refreshed = broker.Register(
            registration with { LeaseDuration = TimeSpan.FromMinutes(15) },
            now.AddMinutes(1));

        Assert.Equal(first.UserId, refreshed.UserId);
        Assert.Equal(first.SessionId, refreshed.SessionId);
        Assert.Equal(now.AddMinutes(16), refreshed.ExpiresAt);
    }

    [Fact]
    public void RegisterRejectsSecondActiveSessionForSameUser()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);

        Assert.Throws<InvalidOperationException>(() =>
            broker.Register(CreateRegistration("user-a", "session-b"), now.AddMinutes(1)));
    }

    [Fact]
    public void ExpiredSessionIsNotResolvedAndCanBeReplaced()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a", TimeSpan.FromMinutes(5)), now);

        DateTimeOffset expiredAt = now.AddMinutes(6);
        Assert.False(broker.TryGet("user-a", expiredAt, out _));

        MagmaEditSessionDescriptor replacement = broker.Register(
            CreateRegistration("user-a", "session-b"),
            expiredAt);

        Assert.Equal("session-b", replacement.SessionId);
    }

    [Fact]
    public void RenewRejectsWrongUserOrSession()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);

        Assert.False(broker.TryRenew("user-b", "session-a", TimeSpan.FromMinutes(10), now.AddMinutes(1), out _));
        Assert.False(broker.TryRenew("user-a", "session-b", TimeSpan.FromMinutes(10), now.AddMinutes(1), out _));
        Assert.True(broker.TryRenew("user-a", "session-a", TimeSpan.FromMinutes(10), now.AddMinutes(1), out MagmaEditSessionDescriptor? renewed));
        Assert.NotNull(renewed);
        Assert.Equal(now.AddMinutes(11), renewed!.ExpiresAt);
    }

    [Fact]
    public void UnregisterRequiresMatchingSessionId()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);

        Assert.False(broker.Unregister("user-a", "session-b"));
        Assert.True(broker.TryGet("user-a", now.AddMinutes(1), out _));
        Assert.True(broker.Unregister("user-a", "session-a"));
        Assert.False(broker.TryGet("user-a", now.AddMinutes(2), out _));
    }

    [Fact]
    public void UnregisterCanRetryAfterConcurrentRenewal()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);

        Assert.True(broker.TryRenew("user-a", "session-a", TimeSpan.FromMinutes(10), now.AddMinutes(1), out _));
        Assert.True(broker.Unregister("user-a", "session-a"));
        Assert.False(broker.TryGet("user-a", now.AddMinutes(2), out _));
    }

    private static MagmaEditSessionRegistration CreateRegistration(
        string userId,
        string sessionId,
        TimeSpan? leaseDuration = null) =>
        new(
            userId,
            sessionId,
            $"connection-{sessionId}",
            "ipc://MagmaEdit.LiveEditor.v1",
            leaseDuration ?? TimeSpan.FromMinutes(15),
            BrokerCapabilities);

    private static readonly string[] BrokerCapabilities =
    [
        "get_editor_state",
        "execute_editor_command"
    ];
}
