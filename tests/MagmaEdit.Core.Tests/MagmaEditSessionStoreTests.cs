using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class MagmaEditSessionStoreTests
{
    [Fact]
    public void BrokerUsesInjectedStore()
    {
        var store = new RecordingSessionStore();
        var broker = new MagmaEditSessionBroker(store);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MagmaEditSessionRegistration registration = new(
            "user-a",
            "session-a",
            "connection-a",
            "ipc://MagmaEdit.LiveEditor.v1",
            TimeSpan.FromMinutes(15),
            ["get_editor_state"]);

        MagmaEditSessionDescriptor expected = new(
            "user-a",
            "session-a",
            "connection-a",
            registration.Endpoint,
            now.AddMinutes(15),
            ["get_editor_state"]);
        store.Descriptor = expected;

        MagmaEditSessionDescriptor actual = broker.Register(registration, now);

        Assert.Equal(expected, actual);
        Assert.Equal(1, store.RegisterCalls);
    }

    private sealed class RecordingSessionStore : IMagmaEditSessionStore
    {
        public MagmaEditSessionDescriptor? Descriptor { get; set; }

        public int RegisterCalls { get; private set; }

        public MagmaEditSessionDescriptor Register(MagmaEditSessionRegistration registration, DateTimeOffset now)
        {
            RegisterCalls++;
            return Descriptor ?? throw new InvalidOperationException("A test descriptor is required.");
        }

        public bool TryGet(string userId, DateTimeOffset now, out MagmaEditSessionDescriptor? descriptor)
        {
            descriptor = Descriptor;
            return descriptor is not null;
        }

        public bool TryRenew(string userId, string sessionId, TimeSpan leaseDuration, DateTimeOffset now, out MagmaEditSessionDescriptor? renewed)
        {
            renewed = Descriptor;
            return renewed is not null;
        }

        public bool Unregister(string userId, string sessionId) => true;
    }
}
