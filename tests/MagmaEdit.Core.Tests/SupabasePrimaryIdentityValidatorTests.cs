using System.Net;
using System.Net.Http.Json;
using MagmaEdit.Broker;

namespace MagmaEdit.Core.Tests;

public sealed class SupabasePrimaryIdentityValidatorTests
{
    [Fact]
    public async Task ValidBearerReturnsSupabaseUserId()
    {
        RecordingHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = " user-123 " })
            }
        };
        using HttpClient httpClient = new(handler);
        SupabasePrimaryIdentityValidator validator = new(
            httpClient,
            new SupabasePrimaryIdentityValidatorOptions(
                "https://project.supabase.co/",
                "publishable-key"));

        string? userId = await validator.ValidateAsync("Bearer access-token");

        Assert.Equal("user-123", userId);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal(
            "https://project.supabase.co/auth/v1/user",
            handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("access-token", handler.Request.Headers.Authorization.Parameter);
        Assert.Equal("publishable-key", handler.Request.Headers.GetValues("apikey").Single());
    }

    [Fact]
    public async Task UnauthorizedResponseReturnsNull()
    {
        RecordingHandler handler = new()
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        using HttpClient httpClient = new(handler);
        SupabasePrimaryIdentityValidator validator = new(
            httpClient,
            new SupabasePrimaryIdentityValidatorOptions(
                "https://project.supabase.co",
                "publishable-key"));

        Assert.Null(await validator.ValidateAsync("Bearer invalid"));
    }

    [Fact]
    public async Task InvalidAuthorizationIsRejectedBeforeNetworkCall()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);
        SupabasePrimaryIdentityValidator validator = new(
            httpClient,
            new SupabasePrimaryIdentityValidatorOptions(
                "https://project.supabase.co",
                "publishable-key"));

        Assert.Null(await validator.ValidateAsync("Basic value"));
        Assert.Null(handler.Request);
    }

    [Fact]
    public void SupabaseUrlMustUseHttps()
    {
        Assert.Throws<ArgumentException>(() => new SupabasePrimaryIdentityValidatorOptions(
            "http://project.supabase.co",
            "publishable-key"));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }
}
