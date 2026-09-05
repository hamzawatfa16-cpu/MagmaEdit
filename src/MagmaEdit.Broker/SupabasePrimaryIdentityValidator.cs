using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MagmaEdit.Broker;

public sealed class SupabasePrimaryIdentityValidatorOptions
{
    public SupabasePrimaryIdentityValidatorOptions(string supabaseUrl, string publishableKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supabaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(publishableKey);

        if (!Uri.TryCreate(supabaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out Uri? uri)
            || uri is null
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The Supabase URL must be an absolute HTTPS URL.", nameof(supabaseUrl));
        }

        SupabaseUrl = uri;
        PublishableKey = publishableKey.Trim();
    }

    public Uri SupabaseUrl { get; }
    public string PublishableKey { get; }
}

/// <summary>Validates an upstream Supabase access token against the Supabase Auth user endpoint.</summary>
public sealed class SupabasePrimaryIdentityValidator : IMagmaEditPrimaryIdentityValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly SupabasePrimaryIdentityValidatorOptions _options;

    public SupabasePrimaryIdentityValidator(
        HttpClient httpClient,
        SupabasePrimaryIdentityValidatorOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask<string?> ValidateAsync(
        string? authorization,
        CancellationToken cancellationToken = default)
    {
        if (!AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return null;
        }

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            new Uri(_options.SupabaseUrl, "auth/v1/user"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter.Trim());
        request.Headers.Add("apikey", _options.PublishableKey);

        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Supabase Auth user validation failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        SupabaseUserResponse? user = await JsonSerializer
            .DeserializeAsync<SupabaseUserResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(user?.Id) ? null : user.Id.Trim();
    }

    private sealed record SupabaseUserResponse(string? Id);
}
