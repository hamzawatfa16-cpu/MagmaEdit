using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class MagmaEditSessionBrokerTests
{
    [Fact]
    public void RegisterAndResolve_ReturnsActiveSessionForSameUser()
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
    public void Register_RejectsSecondActiveSessionForSameUser()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);

        Assert.Throws<InvalidOperationException>(() =>
            broker.Register(CreateRegistration("user-a", "session-b"), now.AddMinutes(1)));
    }

    [Fact]
    public void ExpiredSession_IsNotResolvedAndCanBeReplaced()
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
    public void Renew_RejectsWrongUserOrSession()
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
    public void Unregister_RequiresMatchingSessionId()
    {
        var broker = new MagmaEditSessionBroker();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);

        Assert.False(broker.Unregister("user-a", "session-b"));
        Assert.True(broker.TryGet("user-a", now.AddMinutes(1), out _));
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
            new[] { "get_editor_state", "execute_editor_command" });
}
