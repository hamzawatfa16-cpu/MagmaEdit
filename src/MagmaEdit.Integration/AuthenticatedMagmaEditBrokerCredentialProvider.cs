using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MagmaEdit.Integration;

/// <summary>
/// Obtains a short-lived broker credential from an upstream authenticated access token and keeps only the
/// short-lived broker credential in memory. No credential is persisted to disk.
/// </summary>
public sealed class AuthenticatedMagmaEditBrokerCredentialProvider : IMagmaEditBrokerCredentialProvider, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly Uri _issueUri;
    private readonly Func<CancellationToken, ValueTask<string?>> _upstreamAccessTokenProvider;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _refreshSkew;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MagmaEditBrokerCredential? _cachedCredential;
    private int _disposed;

    public AuthenticatedMagmaEditBrokerCredentialProvider(
        HttpClient httpClient,
        Uri brokerBaseUri,
        Func<CancellationToken, ValueTask<string?>> upstreamAccessTokenProvider,
        TimeProvider? timeProvider = null,
        TimeSpan? refreshSkew = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(brokerBaseUri);
        if (!string.Equals(brokerBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The broker base URI must use HTTPS.", nameof(brokerBaseUri));
        }

        _upstreamAccessTokenProvider = upstreamAccessTokenProvider ?? throw new ArgumentNullException(nameof(upstreamAccessTokenProvider));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _refreshSkew = refreshSkew ?? TimeSpan.FromSeconds(30);
        if (_refreshSkew <= TimeSpan.Zero || _refreshSkew >= TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(refreshSkew), "The refresh skew must be greater than zero and shorter than one hour.");
        }

        _issueUri = new Uri(brokerBaseUri, "v1/broker-credentials/issue");
    }

    public async ValueTask<MagmaEditBrokerCredential> GetCredentialAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        MagmaEditBrokerCredential? cached = _cachedCredential;
        if (cached is not null && cached.ExpiresAt > now.Add(_refreshSkew))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = _timeProvider.GetUtcNow();
            cached = _cachedCredential;
            if (cached is not null && cached.ExpiresAt > now.Add(_refreshSkew))
            {
                return cached;
            }

            string? upstreamAccessToken = await _upstreamAccessTokenProvider(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(upstreamAccessToken))
            {
                throw new InvalidOperationException("No authenticated upstream access token is available for broker credential issuance.");
            }

            using HttpRequestMessage request = new(HttpMethod.Post, _issueUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", upstreamAccessToken.Trim());
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateTransportException(response.StatusCode, responseBody);
            }

            MagmaEditBrokerCredential? issued = JsonSerializer.Deserialize<MagmaEditBrokerCredential>(responseBody, JsonOptions);
            if (issued is null || string.IsNullOrWhiteSpace(issued.AccessToken) || issued.ExpiresAt <= now)
            {
                throw new InvalidDataException("The broker returned an invalid credential response.");
            }

            _cachedCredential = issued;
            return issued;
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _gate.Dispose();
            _cachedCredential = null;
        }

        return ValueTask.CompletedTask;
    }

    private static HttpRequestException CreateTransportException(HttpStatusCode statusCode, string responseBody) =>
        new($"The MagmaEdit broker credential endpoint rejected the request with HTTP {(int)statusCode} ({statusCode}).");
}
