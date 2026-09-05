using MagmaEdit.Auth;
using MagmaEdit.Integration;

namespace MagmaEdit.App;

/// <summary>Owns the optional authenticated desktop-to-broker session for the running editor.</summary>
public sealed class BrokerSessionRuntime : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticatedMagmaEditBrokerCredentialProvider _credentialProvider;
    private readonly MagmaEditDesktopSessionConnectionManager _connectionManager;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _runTask;
    private int _disposed;

    private BrokerSessionRuntime(
        HttpClient httpClient,
        AuthenticatedMagmaEditBrokerCredentialProvider credentialProvider,
        MagmaEditDesktopSessionConnectionManager connectionManager)
    {
        _httpClient = httpClient;
        _credentialProvider = credentialProvider;
        _connectionManager = connectionManager;
        _runTask = RunAsync();
    }

    public MagmaEditDesktopSessionState State => _connectionManager.State;

    public static BrokerSessionRuntime Start(
        AuthSession authSession,
        Func<CancellationToken, ValueTask<string?>> upstreamAccessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(authSession);
        ArgumentNullException.ThrowIfNull(upstreamAccessTokenProvider);

        string? brokerUrl = Environment.GetEnvironmentVariable("MAGMAEDIT_BROKER_URL")?.Trim();
        if (string.IsNullOrWhiteSpace(brokerUrl))
        {
            throw new InvalidOperationException(
                "MAGMAEDIT_BROKER_URL is required to enable the hosted broker session.");
        }

        if (!Uri.TryCreate(brokerUrl, UriKind.Absolute, out Uri? brokerUri)
            || brokerUri is null
            || !string.Equals(brokerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("MAGMAEDIT_BROKER_URL must be an absolute HTTPS URL.");
        }

        string? endpoint = Environment.GetEnvironmentVariable("MAGMAEDIT_DESKTOP_ENDPOINT")?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint)
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri)
            || endpointUri is null
            || (endpointUri.Scheme is not Uri.UriSchemeHttps and not "wss"))
        {
            throw new InvalidOperationException(
                "MAGMAEDIT_DESKTOP_ENDPOINT must be an absolute HTTPS or WSS URL when broker connectivity is enabled.");
        }

        int leaseMinutes = ReadLeaseMinutes();
        TimeSpan leaseDuration = TimeSpan.FromMinutes(leaseMinutes);
        var registration = new MagmaEditSessionRegistration(
            authSession.UserId,
            Guid.NewGuid().ToString("N"),
            $"{Environment.MachineName}-{Guid.NewGuid():N}",
            endpointUri.ToString(),
            leaseDuration,
            [
                nameof(EditorCommandCapability.TimelineEditing),
                nameof(EditorCommandCapability.MediaManagement),
                nameof(EditorCommandCapability.History)
            ]);

        HttpClient httpClient = new();
        var credentialProvider = new AuthenticatedMagmaEditBrokerCredentialProvider(
            httpClient,
            brokerUri,
            upstreamAccessTokenProvider);
        var brokerClient = new AuthenticatedMagmaEditSessionBrokerClient(
            httpClient,
            brokerUri,
            credentialProvider);
        var connectionManager = new MagmaEditDesktopSessionConnectionManager(
            brokerClient,
            registration,
            heartbeatInterval: TimeSpan.FromMinutes(Math.Min(5, Math.Max(1, leaseMinutes / 3))));

        return new BrokerSessionRuntime(httpClient, credentialProvider, connectionManager);
    }

    private async Task RunAsync()
    {
        try
        {
            await _connectionManager.RunAsync(_cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteComponentFailure("broker session", exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        try
        {
            await _runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            if (_connectionManager.CurrentSession is not null)
            {
                await _connectionManager.RevokeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteComponentFailure("broker session shutdown", exception);
        }
        finally
        {
            await _credentialProvider.DisposeAsync().ConfigureAwait(false);
            _httpClient.Dispose();
            _cancellation.Dispose();
            await _connectionManager.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static int ReadLeaseMinutes()
    {
        string? value = Environment.GetEnvironmentVariable("MAGMAEDIT_BROKER_LEASE_MINUTES")?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return 15;
        }

        if (!int.TryParse(value, out int minutes) || minutes is < 5 or > 60)
        {
            throw new InvalidOperationException(
                "MAGMAEDIT_BROKER_LEASE_MINUTES must be an integer between 5 and 60.");
        }

        return minutes;
    }
}
