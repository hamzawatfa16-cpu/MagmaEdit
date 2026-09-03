namespace MagmaEdit.Auth;

public interface IAuthService : IAsyncDisposable
{
    AuthSession? CurrentSession { get; }

    Task<AuthResult> RestoreSessionAsync(CancellationToken cancellationToken = default);

    Task<AuthResult> SignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthResult> SignUpAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
