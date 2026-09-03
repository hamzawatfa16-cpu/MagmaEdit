using System.Diagnostics;
using System.Text;
using Supabase.Gotrue;
using Supabase.Gotrue.Constants;
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

            if (!callback.TryGetAuthorizationError(callbackUri, out string error))
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

    private static AuthSession MapSession(Session session)
    {
        if (session.User is null ||
            string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken) ||
            string.IsNullOrWhiteSpace(session.User.Id) ||
            string.IsNullOrWhiteSpace(session.User.Email))
        {
            throw new InvalidDataException("Supabase returned an incomplete authentication session.");
        }

        long expiresAtSeconds = session.ExpiresAt;
        if (expiresAtSeconds <= 0)
        {
            throw new InvalidDataException("Supabase returned an invalid session expiration time.");
        }

        return new AuthSession(
            session.AccessToken,
            session.RefreshToken,
            session.User.Id,
            session.User.Email,
            DateTimeOffset.FromUnixTimeSeconds(expiresAtSeconds));
    }

    private static Uri CreateSupabaseUri(string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out Uri? uri) || uri is null ||
            (uri.Scheme is not "https" and not "http"))
        {
            throw new ArgumentException("The Supabase URL must be an absolute HTTP or HTTPS URL.", nameof(value));
        }

        return uri;
    }

    private static void OpenBrowser(Uri uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.ToString(),
            UseShellExecute = true
        });
    }

    private static string? GetQueryParameter(Uri uri, string name) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .Select(pair => new
            {
                Key = Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                Value = Uri.UnescapeDataString(pair[1].Replace('+', ' '))
            })
            .FirstOrDefault(item => string.Equals(item.Key, name, StringComparison.Ordinal))?.Value;

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class CallbackListener : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _prefix;

        private CallbackListener(HttpListener listener, string prefix)
        {
            _listener = listener;
            _prefix = prefix;
        }

        public string RedirectUri => _prefix + "oauth/callback";

        public static CallbackListener Start(string host)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int port = Random.Shared.Next(49152, 65535);
                string prefix = $"http://{host}:{port}/";
                HttpListener listener = new();
                try
                {
                    listener.Prefixes.Add(prefix);
                    listener.Start();
                    return new CallbackListener(listener, prefix);
                }
                catch (HttpListenerException)
                {
                    listener.Close();
                }
            }

            throw new InvalidOperationException("Could not reserve a local authentication callback port.");
        }

        public async Task<Uri> WaitForCallbackAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeoutCancellation = new(timeout);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCancellation.Token,
                cancellationToken);

            try
            {
                HttpListenerContext context = await _listener.GetContextAsync()
                    .WaitAsync(linked.Token)
                    .ConfigureAwait(false);

                Uri callbackUri = context.Request.Url
                    ?? throw new InvalidDataException("The authentication callback did not contain a URL.");

                byte[] response = Encoding.UTF8.GetBytes(
                    "<html><body><p>MagmaEdit sign-in complete. You can close this window.</p></body></html>");
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = response.Length;
                await context.Response.OutputStream.WriteAsync(response, CancellationToken.None).ConfigureAwait(false);
                context.Response.Close();

                return callbackUri;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException("The authentication callback timed out.");
            }
        }

        public bool TryGetAuthorizationError(Uri uri, out string error)
        {
            string? code = GetQueryParameter(uri, "error");
            string? description = GetQueryParameter(uri, "error_description");
            error = string.IsNullOrWhiteSpace(description)
                ? code ?? string.Empty
                : description;
            return !string.IsNullOrWhiteSpace(error);
        }

        public void Dispose() => _listener.Close();
    }
}
