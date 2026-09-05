using System.Net;
using System.Net.Http;
using System.Text;
using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class AuthenticatedMagmaEditBrokerCredentialProviderTests
{
    [Fact]
    public async Task IssuesCredentialFromUpstreamTokenAndCachesUntilRefreshWindow()
    {
        int requests = 0;
        var handler = new StubHandler((request, _) =>
        {
            requests++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://broker.example.test/v1/broker-credentials/issue", request.RequestUri!.ToString());
            Assert.Equal("Bearer upstream-token", request.Headers.Authorization!.ToString());
            return Task.FromResult(CreateResponse("{\"accessToken\":\"broker-token\",\"expiresAt\":\"2030-01-01T00:10:00Z\"}"));
        });

        using HttpClient httpClient = new(handler);
        await using var provider = new AuthenticatedMagmaEditBrokerCredentialProvider(
            httpClient,
            new Uri("https://broker.example.test/"),
            _ => ValueTask.FromResult<string?>("upstream-token"),
            new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        MagmaEditBrokerCredential first = await provider.GetCredentialAsync();
        MagmaEditBrokerCredential second = await provider.GetCredentialAsync();

        Assert.Equal("broker-token", first.AccessToken);
        Assert.Equal(first, second);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task RefreshesCredentialInsideRefreshWindow()
    {
        int requests = 0;
        var handler = new StubHandler((_, _) =>
        {
            requests++;
            string token = requests == 1 ? "broker-token-1" : "broker-token-2";
            return Task.FromResult(CreateResponse($"{{\"accessToken\":\"{token}\",\"expiresAt\":\"2030-01-01T00:01:00Z\"}}"));
        });

        using HttpClient httpClient = new(handler);
        var clock = new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var provider = new AuthenticatedMagmaEditBrokerCredentialProvider(
            httpClient,
            new Uri("https://broker.example.test/"),
            _ => ValueTask.FromResult<string?>("upstream-token"),
            clock,
            TimeSpan.FromSeconds(30));

        MagmaEditBrokerCredential first = await provider.GetCredentialAsync();
        clock.UtcNowValue = new DateTimeOffset(2030, 1, 1, 0, 0, 31, TimeSpan.Zero);
        MagmaEditBrokerCredential second = await provider.GetCredentialAsync();

        Assert.Equal("broker-token-1", first.AccessToken);
        Assert.Equal("broker-token-2", second.AccessToken);
        Assert.Equal(2, requests);
    }

    private static HttpResponseMessage CreateResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public FixedTimeProvider(DateTimeOffset utcNow) => UtcNowValue = utcNow;

        public DateTimeOffset UtcNowValue { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }
}
