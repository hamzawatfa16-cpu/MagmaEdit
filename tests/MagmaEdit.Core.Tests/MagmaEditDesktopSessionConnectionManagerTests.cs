using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class MagmaEditDesktopSessionConnectionManagerTests
{
    [Fact]
    public async Task RunRegistersAndRenewsSession()
    {
        var broker = new FakeSessionBrokerClient();
        MagmaEditSessionRegistration registration = CreateRegistration();
        await using var manager = new MagmaEditDesktopSessionConnectionManager(
            broker,
            registration,
            heartbeatInterval: TimeSpan.FromMilliseconds(20),
            retryDelay: TimeSpan.FromMilliseconds(10));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(120));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RunAsync(cancellation.Token));

        Assert.NotNull(manager.CurrentSession);
        Assert.Equal(MagmaEditDesktopSessionState.Connected, manager.State);
        Assert.True(broker.RegisterCount >= 1);
        Assert.True(broker.RenewCount >= 1);
    }

    [Fact]
    public async Task FailedRegistrationRetriesWithoutChangingToConnected()
    {
        var broker = new FakeSessionBrokerClient { FailRegistrations = 1 };
        MagmaEditSessionRegistration registration = CreateRegistration();
        await using var manager = new MagmaEditDesktopSessionConnectionManager(
            broker,
            registration,
            heartbeatInterval: TimeSpan.FromMilliseconds(50),
            retryDelay: TimeSpan.FromMilliseconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(120));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RunAsync(cancellation.Token));

        Assert.True(broker.RegisterCount >= 2);
        Assert.Equal(MagmaEditDesktopSessionState.Connected, manager.State);
    }

    [Fact]
    public async Task FailedRenewalCausesReconnect()
    {
        var broker = new FakeSessionBrokerClient { FailRenewals = 1 };
        MagmaEditSessionRegistration registration = CreateRegistration();
        await using var manager = new MagmaEditDesktopSessionConnectionManager(
            broker,
            registration,
            heartbeatInterval: TimeSpan.FromMilliseconds(20),
            retryDelay: TimeSpan.FromMilliseconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(140));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RunAsync(cancellation.Token));

        Assert.True(broker.RegisterCount >= 2);
        Assert.True(broker.RenewCount >= 1);
        Assert.Equal(MagmaEditDesktopSessionState.Connected, manager.State);
    }

    [Fact]
    public async Task RevokeUnregistersCurrentSession()
    {
        var broker = new FakeSessionBrokerClient();
        MagmaEditSessionRegistration registration = CreateRegistration();
        await using var manager = new MagmaEditDesktopSessionConnectionManager(
            broker,
            registration,
            heartbeatInterval: TimeSpan.FromMilliseconds(100));
        using var cancellation = new CancellationTokenSource();

        Task run = manager.RunAsync(cancellation.Token);
        await WaitUntilAsync(() => manager.CurrentSession is not null);

        Assert.True(await manager.RevokeAsync());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal(1, broker.UnregisterCount);
        Assert.Equal(MagmaEditDesktopSessionState.Revoked, manager.State);
    }

    private static MagmaEditSessionRegistration CreateRegistration() =>
        new(
            "user-a",
            "session-a",
            "connection-a",
            "wss://broker.example/desktop",
            TimeSpan.FromSeconds(1),
            ["get_editor_state", "execute_editor_command"]);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!predicate() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.True(predicate());
    }

    private sealed class FakeSessionBrokerClient : IMagmaEditSessionBrokerClient
    {
        private readonly object _sync = new();
        private readonly MagmaEditSessionBroker _broker = new();
        private int _failRegistrations;
        private int _failRenewals;

        public int FailRegistrations
        {
            get => Volatile.Read(ref _failRegistrations);
            init => _failRegistrations = value;
        }

        public int FailRenewals
        {
            get => Volatile.Read(ref _failRenewals);
            init => _failRenewals = value;
        }

        public int RegisterCount { get; private set; }
        public int RenewCount { get; private set; }
        public int UnregisterCount { get; private set; }

        public Task<MagmaEditSessionDescriptor> RegisterAsync(
            MagmaEditSessionRegistration registration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                RegisterCount++;
                if (Volatile.Read(ref _failRegistrations) > 0)
                {
                    Interlocked.Decrement(ref _failRegistrations);
                    throw new IOException("simulated registration failure");
                }

                return Task.FromResult(_broker.Register(registration, DateTimeOffset.UtcNow));
            }
        }

        public Task<MagmaEditSessionDescriptor?> RenewAsync(
            string userId,
            string sessionId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                RenewCount++;
                if (Volatile.Read(ref _failRenewals) > 0)
                {
                    Interlocked.Decrement(ref _failRenewals);
                    throw new IOException("simulated renewal failure");
                }

                _broker.TryRenew(userId, sessionId, leaseDuration, DateTimeOffset.UtcNow, out MagmaEditSessionDescriptor? renewed);
                return Task.FromResult(renewed);
            }
        }

        public Task<bool> UnregisterAsync(
            string userId,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                UnregisterCount++;
                return Task.FromResult(_broker.Unregister(userId, sessionId));
            }
        }
    }
}
