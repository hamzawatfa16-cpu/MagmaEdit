using System.Security.Cryptography;
using System.Text;

namespace MagmaEdit.AiBridge;

public static class AiBridgeSecurity
{
    public static bool HasValidBearerToken(string? authorization, string expectedToken)
    {
        return TryGetBearerToken(authorization, out string suppliedToken)
            && HasValidToken(suppliedToken, expectedToken);
    }

    public static bool TryGetBearerToken(string? authorization, out string token)
    {
        const string prefix = "Bearer ";

        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith(prefix, StringComparison.Ordinal))
        {
            token = string.Empty;
            return false;
        }

        token = authorization[prefix.Length..];
        return token.Length > 0;
    }

    public static bool HasValidToken(string suppliedToken, string expectedToken)
    {
        if (suppliedToken.Length == 0 || expectedToken.Length == 0 || suppliedToken.Length != expectedToken.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedToken),
            Encoding.UTF8.GetBytes(expectedToken));
    }

    public static bool IsUserAllowed(string userId, IReadOnlySet<string> allowedUserIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(allowedUserIds);
        return allowedUserIds.Count == 0 || allowedUserIds.Contains(userId);
    }

    public static bool IsMutationAllowed(
        bool requested,
        bool serverEnabled,
        string userId,
        IReadOnlySet<string> allowedUserIds)
    {
        if (!requested)
            return true;

        return serverEnabled && allowedUserIds.Count == 1 && allowedUserIds.Contains(userId);
    }
}
