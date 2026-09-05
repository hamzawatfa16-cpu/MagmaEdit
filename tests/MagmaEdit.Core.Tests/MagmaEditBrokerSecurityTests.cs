using System.Globalization;
using MagmaEdit.Broker;

namespace MagmaEdit.Core.Tests;

public sealed class MagmaEditBrokerSecurityTests
{
    [Fact]
    public void CredentialStoreBindsCredentialToUserAndRevocationIsEffective()
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var store = new InMemoryMagmaEditBrokerCredentialStore();

        MagmaEditBrokerCredentialIssue issued = store.Issue(" user-a ", now, TimeSpan.FromMinutes(10));

        Assert.False(string.IsNullOrWhiteSpace(issued.AccessToken));
        Assert.True(store.TryAuthenticate(issued.AccessToken, now.AddMinutes(9), out string? userId));
        Assert.Equal("user-a", userId);
        Assert.False(store.TryAuthenticate(issued.AccessToken, now.AddMinutes(11), out _));
        Assert.True(store.Revoke(issued.AccessToken, "user-a"));
        Assert.False(store.TryAuthenticate(issued.AccessToken, now.AddMinutes(9), out _));
        Assert.False(store.Revoke(issued.AccessToken, "user-a"));
    }

    [Fact]
    public void RevocationCannotBeAppliedByAnotherUser()
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var store = new InMemoryMagmaEditBrokerCredentialStore();
        MagmaEditBrokerCredentialIssue issued = store.Issue("user-a", now, TimeSpan.FromMinutes(10));

        Assert.False(store.Revoke(issued.AccessToken, "user-b"));
        Assert.True(store.TryAuthenticate(issued.AccessToken, now, out string? userId));
        Assert.Equal("user-a", userId);
    }

    [Fact]
    public void ReplayProtectorAcceptsRequestOnceWithinClockSkew()
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var protector = new InMemoryMagmaEditReplayProtector(TimeSpan.FromMinutes(5));
        string timestamp = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        Assert.True(protector.TryAccept("request-1", timestamp, now));
        Assert.False(protector.TryAccept("request-1", timestamp, now));
        Assert.True(protector.TryAccept("request-2", timestamp, now));
    }

    [Fact]
    public void ReplayProtectorRejectsStaleAndFutureRequests()
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var protector = new InMemoryMagmaEditReplayProtector(TimeSpan.FromMinutes(5));
        string stale = now.AddMinutes(-6).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        string future = now.AddMinutes(6).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        Assert.False(protector.TryAccept("stale", stale, now));
        Assert.False(protector.TryAccept("future", future, now));
        Assert.False(protector.TryAccept("bad", "not-a-timestamp", now));
    }

    [Fact]
    public void CredentialLifetimeMustBeShortLived()
    {
        var store = new InMemoryMagmaEditBrokerCredentialStore();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Issue("user-a", now, TimeSpan.FromHours(2)));
    }
}
