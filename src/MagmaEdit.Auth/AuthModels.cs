namespace MagmaEdit.Auth;

public sealed record AuthSession(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string Email,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc;
}

public sealed record AuthUser(
    string UserId,
    string Email);

public sealed record AuthResult(
    bool Succeeded,
    string Message,
    AuthSession? Session = null,
    AuthUser? User = null)
{
    public static AuthResult Success(AuthSession session) =>
        new(true, "Authentication successful.", session, new AuthUser(session.UserId, session.Email));

    public static AuthResult Failure(string message) =>
        new(false, message);
}
