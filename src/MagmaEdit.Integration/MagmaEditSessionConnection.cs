namespace MagmaEdit.Integration;

/// <summary>Lifecycle state of an authenticated desktop session connection.</summary>
public enum MagmaEditDesktopSessionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Revoked
}

/// <summary>Provider-neutral broker operations used by the desktop session connection manager.</summary>
public interface IMagmaEditSessionBrokerClient
{
    Task<MagmaEditSessionDescriptor> RegisterAsync(
        MagmaEditSessionRegistration registration,
        CancellationToken cancellationToken = default);

    Task<MagmaEditSessionDescriptor?> RenewAsync(
        string userId,
        string sessionId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> UnregisterAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>Controls one authenticated desktop session lease and its heartbeat/reconnect lifecycle.</summary>
public sealed class MagmaEditDesktopSessionConnectionManager : IAsyncDisposable
{
    private readonly IMagmaEditSessionBrokerClient _broker;
    private readonly MagmaEditSessionRegistration _registration;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _retryDelay;
    private int _state = (int)MagmaEditDesktopSessionState.Disconnected;
    private int _disposed;

    public MagmaEditDesktopSessionConnectionManager(
        IMagmaEditSessionBrokerClient broker,
        MagmaEditSessionRegistration registration,
        TimeProvider? timeProvider = null,
        TimeSpan? heartbeatInterval = null,
        TimeSpan? retryDelay = null)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        ArgumentNullException.ThrowIfNull(registration);
        _registration = registration;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromMinutes(5);
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
        ValidateIntervals(_heartbeatInterval, _retryDelay, registration.LeaseDuration);
    }

    public MagmaEditDesktopSessionState State =>
        (MagmaEditDesktopSessionState)Volatile.Read(ref _state);

    public MagmaEditSessionDescriptor? CurrentSession { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        while (!cancellationToken.IsCancellationRequested)
        {
            MagmaEditSessionDescriptor? session = await TryRegisterAsync(cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                if (await DelayAsync(_retryDelay, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }

            CurrentSession = session;
            SetState(MagmaEditDesktopSessionState.Connected);
            bool shouldReconnect = await RunHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            CurrentSession = null;

            if (!shouldReconnect)
            {
                break;
            }
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            SetState(MagmaEditDesktopSessionState.Disconnected);
        }
    }

    public async Task<bool> RevokeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        MagmaEditSessionDescriptor? session = CurrentSession;
        if (session is null)
        {
            SetState(MagmaEditDesktopSessionState.Revoked);
            return false;
        }

        bool removed = await _broker.UnregisterAsync(
            session.UserId,
            session.SessionId,
            cancellationToken).ConfigureAwait(false);
        CurrentSession = null;
        SetState(MagmaEditDesktopSessionState.Revoked);
        return removed;
    }

    private async Task<MagmaEditSessionDescriptor?> TryRegisterAsync(CancellationToken cancellationToken)
    {
        SetState(MagmaEditDesktopSessionState.Connecting);
        try
        {
            return await _broker.RegisterAsync(_registration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SetState(MagmaEditDesktopSessionState.Reconnecting);
            return null;
        }
        catch (TimeoutException)
        {
            SetState(MagmaEditDesktopSessionState.Reconnecting);
            return null;
        }
        catch (IOException)
        {
            SetState(MagmaEditDesktopSessionState.Reconnecting);
            return null;
        }
    }

    private async Task<bool> RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(_heartbeatInterval, _timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            MagmaEditSessionDescriptor? session = CurrentSession;
            if (session is null)
            {
                return true;
            }

            try
            {
                MagmaEditSessionDescriptor? renewed = await _broker.RenewAsync(
                    session.UserId,
                    session.SessionId,
                    _registration.LeaseDuration,
                    cancellationToken).ConfigureAwait(false);
                if (renewed is null)
                {
                    SetState(MagmaEditDesktopSessionState.Reconnecting);
                    return true;
                }

                CurrentSession = renewed;
                SetState(MagmaEditDesktopSessionState.Connected);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                SetState(MagmaEditDesktopSessionState.Reconnecting);
                return true;
            }
            catch (TimeoutException)
            {
                SetState(MagmaEditDesktopSessionState.Reconnecting);
                return true;
            }
            catch (IOException)
            {
                SetState(MagmaEditDesktopSessionState.Reconnecting);
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }
    }

    private void SetState(MagmaEditDesktopSessionState state) =>
        Interlocked.Exchange(ref _state, (int)state);

    private static void ValidateIntervals(TimeSpan heartbeatInterval, TimeSpan retryDelay, TimeSpan leaseDuration)
    {
        if (heartbeatInterval <= TimeSpan.Zero || heartbeatInterval >= leaseDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heartbeatInterval),
                "The heartbeat interval must be greater than zero and shorter than the session lease.");
        }

        if (retryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay), "The retry delay must be greater than zero.");
        }
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }
}
