using System.Collections.Concurrent;

namespace MagmaEdit.Integration;

/// <summary>Development-only process-local implementation of the durable session store contract.</summary>
public sealed class InMemoryMagmaEditSessionStore : IMagmaEditSessionStore
{
    private readonly ConcurrentDictionary<string, MagmaEditSessionDescriptor> _sessions = new(StringComparer.Ordinal);

    public MagmaEditSessionDescriptor Register(MagmaEditSessionRegistration registration, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ValidateRegistration(registration);

        MagmaEditSessionDescriptor descriptor = new(
            registration.UserId.Trim(),
            registration.SessionId.Trim(),
            registration.ConnectionId.Trim(),
            registration.Endpoint.Trim(),
            now.Add(registration.LeaseDuration),
            registration.Capabilities
                .Where(static capability => !string.IsNullOrWhiteSpace(capability))
                .Select(static capability => capability.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        while (true)
        {
            if (_sessions.TryGetValue(descriptor.UserId, out MagmaEditSessionDescriptor? existing))
            {
                if (existing.ExpiresAt <= now ||
                    (string.Equals(existing.SessionId, descriptor.SessionId, StringComparison.Ordinal) &&
                     string.Equals(existing.ConnectionId, descriptor.ConnectionId, StringComparison.Ordinal)))
                {
                    if (_sessions.TryUpdate(descriptor.UserId, descriptor, existing))
                    {
                        return descriptor;
                    }

                    continue;
                }

                throw new InvalidOperationException("An active editor session is already registered for this user.");
            }

            if (_sessions.TryAdd(descriptor.UserId, descriptor))
            {
                return descriptor;
            }
        }
    }

    public bool TryGet(string userId, DateTimeOffset now, out MagmaEditSessionDescriptor? descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        string normalizedUserId = userId.Trim();

        if (!_sessions.TryGetValue(normalizedUserId, out descriptor))
        {
            return false;
        }

        if (descriptor.ExpiresAt > now)
        {
            return true;
        }

        _sessions.TryRemove(new KeyValuePair<string, MagmaEditSessionDescriptor>(normalizedUserId, descriptor));
        descriptor = null;
        return false;
    }

    public bool TryRenew(
        string userId,
        string sessionId,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        out MagmaEditSessionDescriptor? renewed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ValidateLease(leaseDuration);

        string normalizedUserId = userId.Trim();
        string normalizedSessionId = sessionId.Trim();
        renewed = null;

        while (_sessions.TryGetValue(normalizedUserId, out MagmaEditSessionDescriptor? existing))
        {
            if (existing.ExpiresAt <= now)
            {
                _sessions.TryRemove(new KeyValuePair<string, MagmaEditSessionDescriptor>(normalizedUserId, existing));
                return false;
            }

            if (!string.Equals(existing.SessionId, normalizedSessionId, StringComparison.Ordinal))
            {
                return false;
            }

            MagmaEditSessionDescriptor replacement = existing with { ExpiresAt = now.Add(leaseDuration) };
            if (_sessions.TryUpdate(normalizedUserId, replacement, existing))
            {
                renewed = replacement;
                return true;
            }
        }

        return false;
    }

    public bool Unregister(string userId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        string normalizedUserId = userId.Trim();
        string normalizedSessionId = sessionId.Trim();

        while (_sessions.TryGetValue(normalizedUserId, out MagmaEditSessionDescriptor? existing))
        {
            if (!string.Equals(existing.SessionId, normalizedSessionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (_sessions.TryRemove(new KeyValuePair<string, MagmaEditSessionDescriptor>(normalizedUserId, existing)))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateRegistration(MagmaEditSessionRegistration registration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.ConnectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.Endpoint);
        ArgumentNullException.ThrowIfNull(registration.Capabilities);
        ValidateLease(registration.LeaseDuration);
    }

    private static void ValidateLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The session lease must be greater than zero and no longer than 24 hours.");
        }
    }
}
