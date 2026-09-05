using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MagmaEdit.Broker;

public sealed record MagmaEditBrokerCredentialIssue(string AccessToken, DateTimeOffset ExpiresAt);

public interface IMagmaEditPrimaryIdentityValidator
{
    ValueTask<string?> ValidateAsync(string? authorization, CancellationToken cancellationToken = default);
}

public interface IMagmaEditBrokerCredentialStore
{
    MagmaEditBrokerCredentialIssue Issue(string userId, DateTimeOffset now, TimeSpan lifetime);
    bool TryAuthenticate(string accessToken, DateTimeOffset now, out string? userId);
    bool Revoke(string accessToken, string userId);
}

public interface IMagmaEditBrokerReplayProtector
{
    bool TryAccept(string? requestId, string? timestamp, DateTimeOffset now);
}

public sealed class InMemoryMagmaEditBrokerCredentialStore : IMagmaEditBrokerCredentialStore
{
    private sealed record Entry(string UserId, DateTimeOffset ExpiresAt, bool Revoked);

    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public MagmaEditBrokerCredentialIssue Issue(string userId, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Broker credential lifetime must be greater than zero and no longer than one hour.");
        }

        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        string tokenKey = Hash(token);
        MagmaEditBrokerCredentialIssue result = new(token, now.Add(lifetime));

        lock (_sync)
        {
            _entries[tokenKey] = new Entry(userId.Trim(), result.ExpiresAt, false);
        }

        return result;
    }

    public bool TryAuthenticate(string accessToken, DateTimeOffset now, out string? userId)
    {
        userId = null;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        string tokenKey = Hash(accessToken);
        lock (_sync)
        {
            if (!_entries.TryGetValue(tokenKey, out Entry? entry) || entry.Revoked || entry.ExpiresAt <= now)
            {
                return false;
            }

            userId = entry.UserId;
            return true;
        }
    }

    public bool Revoke(string accessToken, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        string tokenKey = Hash(accessToken);
        lock (_sync)
        {
            if (!_entries.TryGetValue(tokenKey, out Entry? entry)
                || !string.Equals(entry.UserId, userId.Trim(), StringComparison.Ordinal)
                || entry.Revoked)
            {
                return false;
            }

            _entries[tokenKey] = entry with { Revoked = true };
            return true;
        }
    }

    private static string Hash(string token)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(digest);
    }
}

public sealed class InMemoryMagmaEditReplayProtector : IMagmaEditBrokerReplayProtector
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeSpan _clockSkew;

    public InMemoryMagmaEditReplayProtector(TimeSpan? clockSkew = null)
    {
        _clockSkew = clockSkew ?? TimeSpan.FromMinutes(5);
        if (_clockSkew <= TimeSpan.Zero || _clockSkew > TimeSpan.FromMinutes(15))
        {
            throw new ArgumentOutOfRangeException(nameof(clockSkew), "Replay clock skew must be greater than zero and no longer than 15 minutes.");
        }
    }

    public bool TryAccept(string? requestId, string? timestamp, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(requestId)
            || !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixSeconds))
        {
            return false;
        }

        DateTimeOffset requestTime;
        try
        {
            requestTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        if (requestTime < now.Subtract(_clockSkew) || requestTime > now.Add(_clockSkew))
        {
            return false;
        }

        string normalized = requestId.Trim();
        lock (_sync)
        {
            foreach (string key in _seen.Where(pair => pair.Value < now.Subtract(_clockSkew)).Select(pair => pair.Key).ToArray())
            {
                _seen.Remove(key);
            }

            if (_seen.ContainsKey(normalized))
            {
                return false;
            }

            _seen.Add(normalized, requestTime);
            return true;
        }
    }
}

public sealed class RejectingPrimaryIdentityValidator : IMagmaEditPrimaryIdentityValidator
{
    public ValueTask<string?> ValidateAsync(string? authorization, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<string?>(null);
}
