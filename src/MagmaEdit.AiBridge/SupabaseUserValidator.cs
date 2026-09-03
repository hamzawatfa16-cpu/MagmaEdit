using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MagmaEdit.AiBridge;

public sealed class SupabaseUserValidator
{
    private static readonly Uri UserPath = new("auth/v1/user", UriKind.Relative);

    private readonly AiBridgeOptions _options;
    private readonly HttpClient _httpClient;

    public SupabaseUserValidator(AiBridgeOptions options, HttpClient httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<AuthenticatedSupabaseUser?> ValidateAsync(
        string? authorization,
        CancellationToken cancellationToken)
    {
        if (!AiBridgeSecurity.TryGetBearerToken(authorization, out string accessToken))
            return null;

        using HttpRequestMessage request = new(HttpMethod.Get, UserPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("apikey", _options.SupabasePublishableKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        SupabaseUserResponse? user = await response.Content.ReadFromJsonAsync<SupabaseUserResponse>(cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Id))
            return null;

        return new AuthenticatedSupabaseUser(user.Id, user.Email ?? string.Empty);
    }

    private sealed record SupabaseUserResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("email")] string? Email);
}

public sealed record AuthenticatedSupabaseUser(string UserId, string Email);
