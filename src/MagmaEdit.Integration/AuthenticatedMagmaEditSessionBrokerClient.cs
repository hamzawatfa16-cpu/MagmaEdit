using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MagmaEdit.Integration;

/// <summary>Short-lived bearer credential used by the authenticated desktop-to-broker client.</summary>
public sealed record MagmaEditBrokerCredential(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>Resolves a current short-lived broker credential without exposing credential storage to the client.</summary>
public interface IMagmaEditBrokerCredentialProvider
{
    ValueTask<MagmaEditBrokerCredential> GetCredentialAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// HTTPS desktop client for the authenticated session broker. It does not persist credentials and never falls back to HTTP.
/// </summary>
public sealed class AuthenticatedMagmaEditSessionBrokerClient : IMagmaEditSessionBrokerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IMagmaEditBrokerCredentialProvider _credentialProvider;
    private readonly Uri _baseUri;
    private readonly TimeProvider _timeProvider;

    public AuthenticatedMagmaEditSessionBrokerClient(
        HttpClient httpClient,
        Uri baseUri,
        IMagmaEditBrokerCredentialProvider credentialProvider,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The broker base URI must use HTTPS.", nameof(baseUri));
        }

        _baseUri = baseUri;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<MagmaEditSessionDescriptor> RegisterAsync(
        MagmaEditSessionRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return SendAsync<MagmaEditSessionDescriptor>(
            HttpMethod.Post,
            "v1/desktop-sessions/register",
            new RegistrationEnvelope(registration),
            cancellationToken);
    }

    public Task<MagmaEditSessionDescriptor?> RenewAsync(
        string userId,
        string sessionId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(userId, sessionId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The lease duration must be greater than zero.");
        }

        return SendAsync<MagmaEditSessionDescriptor?>(
            HttpMethod.Post,
            "v1/desktop-sessions/renew",
            new RenewalEnvelope(userId, sessionId, leaseDuration),
            cancellationToken);
    }

    public async Task<bool> UnregisterAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(userId, sessionId);
        UnregisterResponse response = await SendAsync<UnregisterResponse>(
            HttpMethod.Post,
            "v1/desktop-sessions/revoke",
            new RevokeEnvelope(userId, sessionId),
            cancellationToken).ConfigureAwait(false);
        return response.Removed;
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        MagmaEditBrokerCredential credential = await _credentialProvider
            .GetCredentialAsync(cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(credential.AccessToken) || credential.ExpiresAt <= now)
        {
            throw new InvalidOperationException("The broker credential is missing or expired.");
        }

        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string timestamp = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        using HttpRequestMessage request = new(method, new Uri(_baseUri, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        request.Headers.Add("X-MagmaEdit-Request-Id", nonce);
        request.Headers.Add("X-MagmaEdit-Timestamp", timestamp);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateTransportException(response.StatusCode, responseBody);
        }

        T? result = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
        return result ?? throw new InvalidOperationException("The broker returned an empty or invalid response.");
    }

    private static Exception CreateTransportException(HttpStatusCode statusCode, string responseBody) =>
        new HttpRequestException(
            $"The MagmaEdit session broker rejected the request with HTTP {(int)statusCode} ({statusCode}).",
            null,
            statusCode);

    private static void ValidateIdentity(string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("The user ID is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("The session ID is required.", nameof(sessionId));
        }
    }

    private sealed record RegistrationEnvelope(MagmaEditSessionRegistration Registration);

    private sealed record RenewalEnvelope(string UserId, string SessionId, TimeSpan LeaseDuration);

    private sealed record RevokeEnvelope(string UserId, string SessionId);

    private sealed record UnregisterResponse(bool Removed);
}
