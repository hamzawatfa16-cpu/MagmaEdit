using System.Security.Cryptography;
using System.Text;

namespace MagmaEdit.AiBridge;

public static class AiBridgeSecurity
{
    public static bool HasValidBearerToken(string? authorization, string expectedToken)
    {
        const string prefix = "Bearer ";

        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string suppliedToken = authorization[prefix.Length..];
        if (suppliedToken.Length == 0 || expectedToken.Length == 0 || suppliedToken.Length != expectedToken.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedToken),
            Encoding.UTF8.GetBytes(expectedToken));
    }

    public static bool IsMutationAllowed(bool requested, bool serverEnabled) => !requested || serverEnabled;
}
