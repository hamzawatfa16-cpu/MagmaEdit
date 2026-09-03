namespace MagmaEdit.Auth;

public interface IAuthService : IAsyncDisposable
{
    AuthSession? CurrentSession { get; }

    Task<AuthResult> RestoreSessionAsync(CancellationToken cancellationToken = default);

    Task<AuthResult> SignInWithGoogleAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
