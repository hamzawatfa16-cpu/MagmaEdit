using System.Net;
using System.Net.Http.Json;
using MagmaEdit.AiBridge;

namespace MagmaEdit.Core.Tests;

public sealed class SupabaseUserValidatorTests
{
    [Fact]
    public async Task ValidAccessTokenProducesAuthenticatedUser()
    {
        StubHandler handler = new(HttpStatusCode.OK, new { id = "user-a", email = "user@example.com" });
        SupabaseUserValidator validator = CreateValidator(handler);

        AuthenticatedSupabaseUser? user = await validator.ValidateAsync("Bearer access-token", CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user-a", user.UserId);
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("access-token", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task InvalidAuthorizationIsRejectedWithoutNetworkCall()
    {
        StubHandler handler = new(HttpStatusCode.OK, new { id = "user-a" });
        SupabaseUserValidator validator = CreateValidator(handler);

        AuthenticatedSupabaseUser? user = await validator.ValidateAsync("Basic access-token", CancellationToken.None);

        Assert.Null(user);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task NonSuccessResponseIsRejected()
    {
        StubHandler handler = new(HttpStatusCode.Unauthorized, null);
        SupabaseUserValidator validator = CreateValidator(handler);

        AuthenticatedSupabaseUser? user = await validator.ValidateAsync("Bearer access-token", CancellationToken.None);

        Assert.Null(user);
    }

    private static SupabaseUserValidator CreateValidator(StubHandler handler)
    {
        AiBridgeOptions options = new()
        {
            SupabasePublishableKey = "publishable-key"
        };
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("https://example.supabase.co/")
        };
        return new SupabaseUserValidator(options, client);
    }

    private sealed class StubHandler(HttpStatusCode statusCode, object? responseBody) : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            HttpResponseMessage response = new(statusCode);
            if (responseBody is not null)
                response.Content = JsonContent.Create(responseBody);

            return await Task.FromResult(response);
        }
    }
}
