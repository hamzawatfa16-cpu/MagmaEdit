using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class MagmaEditSessionTransportTests
{
    [Fact]
    public async Task SendRoutesToRegisteredUserSession()
    {
        var broker = new MagmaEditSessionBroker();
        var transport = new MagmaEditSessionTransportRegistry(broker);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);
        var connection = new TestDesktopSessionConnection("user-a", "session-a");
        transport.Attach(connection, now.AddMinutes(1));

        MagmaEditSessionTransportResponse response = await transport.SendAsync(
            new MagmaEditSessionTransportRequest("user-a", "session-a", "get_editor_state", "{}", "corr-1"));

        Assert.True(response.Succeeded);
        Assert.Equal("corr-1", response.CorrelationId);
        Assert.Equal(1, connection.Requests.Count);
    }

    [Fact]
    public async Task SendRejectsWrongUserForRegisteredSession()
    {
        var broker = new MagmaEditSessionBroker();
        var transport = new MagmaEditSessionTransportRegistry(broker);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);
        transport.Attach(new TestDesktopSessionConnection("user-a", "session-a"), now.AddMinutes(1));

        MagmaEditSessionTransportResponse response = await transport.SendAsync(
            new MagmaEditSessionTransportRequest("user-b", "session-a", "get_editor_state", "{}", "corr-2"));

        Assert.False(response.Succeeded);
        Assert.Equal("corr-2", response.CorrelationId);
    }

    [Fact]
    public void AttachRejectsExpiredSession()
    {
        var broker = new MagmaEditSessionBroker();
        var transport = new MagmaEditSessionTransportRegistry(broker);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a", TimeSpan.FromMinutes(5)), now);

        Assert.Throws<InvalidOperationException>(() =>
            transport.Attach(new TestDesktopSessionConnection("user-a", "session-a"), now.AddMinutes(6)));
    }

    [Fact]
    public async Task DetachedSessionBecomesUnavailable()
    {
        var broker = new MagmaEditSessionBroker();
        var transport = new MagmaEditSessionTransportRegistry(broker);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        broker.Register(CreateRegistration("user-a", "session-a"), now);
        transport.Attach(new TestDesktopSessionConnection("user-a", "session-a"), now.AddMinutes(1));

        Assert.True(transport.Detach("user-a", "session-a"));

        MagmaEditSessionTransportResponse response = await transport.SendAsync(
            new MagmaEditSessionTransportRequest("user-a", "session-a", "get_editor_state", "{}", "corr-3"));

        Assert.False(response.Succeeded);
        Assert.Equal("corr-3", response.CorrelationId);
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
            Capabilities);

    private static readonly string[] Capabilities =
    [
        "get_editor_state",
        "execute_editor_command"
    ];

    private sealed class TestDesktopSessionConnection(string userId, string sessionId)
        : IMagmaEditDesktopSessionConnection
    {
        public string UserId { get; } = userId;
        public string SessionId { get; } = sessionId;
        public List<MagmaEditSessionTransportRequest> Requests { get; } = [];

        public Task<MagmaEditSessionTransportResponse> SendAsync(
            MagmaEditSessionTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new MagmaEditSessionTransportResponse(
                true,
                "Delivered to desktop session.",
                "{}",
                request.CorrelationId));
        }
    }
}
