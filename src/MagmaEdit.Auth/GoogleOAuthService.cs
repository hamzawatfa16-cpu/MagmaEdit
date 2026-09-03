using System.Diagnostics;
using System.Net;
using System.Text;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace MagmaEdit.Auth;

public sealed class GoogleOAuthService : IAuthService
{
    private readonly Client _client;
    private readonly SessionStore _sessionStore;
    private readonly string _redirectHost;
    private readonly int _callbackTimeoutSeconds;
    private bool _disposed;

    public GoogleOAuthService(
        string supabaseUrl,
        string publishableKey,
        SessionStore? sessionStore = null,
        int callbackTimeoutSeconds = 180)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supabaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(publishableKey);
        if (callbackTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callbackTimeoutSeconds));
        }

        Uri root = CreateSupabaseUri(supabaseUrl);
        _client = new Client(new ClientOptions<Session>
        {
            Url = new Uri(root, "auth/v1").ToString().TrimEnd('/'),
            Headers = new Dictionary<string, string>
            {
                ["apikey"] = publishableKey.Trim()
            }
        });
        _sessionStore = sessionStore ?? new SessionStore();
        _redirectHost = "127.0.0.1";
        _callbackTimeoutSeconds = callbackTimeoutSeconds;
    }

    public AuthSession? CurrentSession { get; private set; }

    public Task<AuthResult> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        AuthSession? session = _sessionStore.Load();
        if (session is null)
        {
            CurrentSession = null;
            return Task.FromResult(AuthResult.Failure("No saved authentication session."));
        }

        return RestoreAndRefreshAsync(session, cancellationToken);
    }

    public async Task<AuthResult> SignInWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using CallbackListener callback = CallbackListener.Start(_redirectHost);
        string redirectUri = callback.RedirectUri;

        try
        {
            ProviderAuthState authState = _client.GetUriForProvider(
                Provider.Google,
                new SignInOptions
                {
                    FlowType = OAuthFlowType.PKCE,
                    RedirectTo = redirectUri
                });

            OpenBrowser(authState.Uri);
            Uri callbackUri = await callback.WaitForCallbackAsync(
                TimeSpan.FromSeconds(_callbackTimeoutSeconds),
                cancellationToken).ConfigureAwait(false);

            if (!CallbackListener.TryGetAuthorizationError(callbackUri, out string error))
            {
                string? code = GetQueryParameter(callbackUri, "code");
                if (string.IsNullOrWhiteSpace(code))
                {
                    return AuthResult.Failure("Google sign-in did not return an authorization code.");
                }

                Session? session = await _client.ExchangeCodeForSession(
                    authState.PKCEVerifier ?? throw new InvalidOperationException("Supabase did not provide a PKCE verifier."),
                    code).ConfigureAwait(false);

                if (session is null || session.User is null)
                {
                    return AuthResult.Failure("Google sign-in completed without a usable MagmaEdit session.");
                }

                AuthSession result = MapSession(session);
                _sessionStore.Save(result);
                CurrentSession = result;
                return AuthResult.Success(result);
            }

            return AuthResult.Failure(error);
        }
        catch (GotrueException exception)
        {
            return AuthResult.Failure(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return AuthResult.Failure($"Could not contact the authentication service: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResult.Failure("Google sign-in timed out before the browser callback returned.");
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _client.SignOut().ConfigureAwait(false);
        }
        catch (GotrueException)
        {
            // The local session must still be removed even if the server-side sign-out fails.
        }
        finally
        {
            CurrentSession = null;
            _sessionStore.Delete();
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private async Task<AuthResult> RestoreAndRefreshAsync(
        AuthSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            Session? restored = await _client.SetSession(
                session.AccessToken,
                session.RefreshToken).ConfigureAwait(false);
            if (restored?.User is null)
            {
                _sessionStore.Delete();
                CurrentSession = null;
                return AuthResult.Failure("The saved authentication session is no longer valid.");
            }

            AuthSession refreshed = MapSession(restored);
            _sessionStore.Save(refreshed);
            CurrentSession = refreshed;
            return AuthResult.Success(refreshed);
        }
        catch (GotrueException)
        {
            _sessionStore.Delete();
            CurrentSession = null;
            return AuthResult.Failure("The saved authentication session is no longer valid.");
        }
        catch (HttpRequestException exception)
        {
            return AuthResult.Failure($"Could not restore the authentication session: {exception.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }
