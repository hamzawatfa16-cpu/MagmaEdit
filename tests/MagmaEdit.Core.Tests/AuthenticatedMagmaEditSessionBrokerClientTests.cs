using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class AuthenticatedMagmaEditSessionBrokerClientTests
{
    [Fact]
    public async Task RegisterUsesBearerCredentialAndReplayHeaders()
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);
        AuthenticatedMagmaEditSessionBrokerClient client = new(
            httpClient,
            new Uri("https://broker.example.test/"),
            new StubCredentialProvider(new MagmaEditBrokerCredential("secret-token", now.AddMinutes(5))),
            new FixedTimeProvider(now));

        MagmaEditSessionDescriptor expected = CreateDescriptor(now);
        handler.Response = JsonResponse(expected);

        MagmaEditSessionDescriptor actual = await client.RegisterAsync(CreateRegistration());

        Assert.Equal(expected, actual);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("https://broker.example.test/v1/desktop-sessions/register", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", handler.Request.Headers.Authorization.Parameter);
        Assert.True(handler.Request.Headers.Contains("X-MagmaEdit-Request-Id"));
        Assert.True(handler.Request.Headers.Contains("X-MagmaEdit-Timestamp"));
        Assert.Equal(now.ToUnixTimeSeconds().ToString(), handler.Request.Headers.GetValues("X-MagmaEdit-Timestamp").Single());
    }

    [Fact]
    public async Task ExpiredCredentialIsRejectedBeforeNetworkCall()
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);
        AuthenticatedMagmaEditSessionBrokerClient client = new(
            httpClient,
            new Uri("https://broker.example.test/"),
            new StubCredentialProvider(new MagmaEditBrokerCredential("secret-token", now)),
            new FixedTimeProvider(now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.RegisterAsync(CreateRegistration()));
        Assert.Null(handler.Request);
    }

    [Fact]
    public void HttpBaseUriIsRejected()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);

        Assert.Throws<ArgumentException>(() => new AuthenticatedMagmaEditSessionBrokerClient(
            httpClient,
            new Uri("http://broker.example.test/"),
            new StubCredentialProvider(new MagmaEditBrokerCredential("token", DateTimeOffset.UtcNow))));
    }

    [Fact]
    public async Task FailedResponseDoesNotReturnBrokerPayloadAsExceptionText()
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        RecordingHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { accessToken = "must-not-leak" })
            }
        };
        using HttpClient httpClient = new(handler);
        AuthenticatedMagmaEditSessionBrokerClient client = new(
            httpClient,
            new Uri("https://broker.example.test/"),
            new StubCredentialProvider(new MagmaEditBrokerCredential("secret-token", now.AddMinutes(5))),
            new FixedTimeProvider(now));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RegisterAsync(CreateRegistration()));

        Assert.DoesNotContain("must-not-leak", exception.Message, StringComparison.Ordinal);
    }

    private static MagmaEditSessionRegistration CreateRegistration() => new(
        "user-1",
        "session-1",
        "connection-1",
        "wss://broker.example.test/desktop",
        TimeSpan.FromMinutes(2),
        ["editor"]);

    private static MagmaEditSessionDescriptor CreateDescriptor(DateTimeOffset now) => new(
        "user-1",
        "session-1",
        "connection-1",
        "wss://broker.example.test/desktop",
        now.AddMinutes(2),
        ["editor"]);

    private static HttpResponseMessage JsonResponse(object value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class StubCredentialProvider(MagmaEditBrokerCredential credential) : IMagmaEditBrokerCredentialProvider
    {
        public ValueTask<MagmaEditBrokerCredential> GetCredentialAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(credential);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public HttpResponseMessage Response { get; set; } = JsonResponse(new { });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }
}
