namespace MagmaEdit.Auth;

public sealed class UnavailableAuthService : IAuthService
{
    private readonly string _message;

    public UnavailableAuthService(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _message = message;
    }

    public AuthSession? CurrentSession => null;

    public Task<AuthResult> RestoreSessionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult.Failure(_message));

    public Task<AuthResult> SignInWithGoogleAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult.Failure(_message));

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
