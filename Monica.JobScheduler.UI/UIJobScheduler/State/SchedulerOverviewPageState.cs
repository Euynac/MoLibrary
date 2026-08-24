using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;

namespace Monica.JobScheduler.UI.UIJobScheduler.State;

/// <summary>
/// Creates component-owned scheduler overview sessions from circuit-scoped dependencies.
/// </summary>
internal sealed class SchedulerOverviewPageStateFactory(
    JobSchedulerFacade facade,
    IJobSchedulerUiAccess access,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Creates a fresh overview state owned by one rendered page.
    /// </summary>
    public SchedulerOverviewPageState Create(TimeSpan refreshInterval) =>
        new(facade.GetOverviewAsync, access.IsAuthorizedAsync, timeProvider, refreshInterval);
}

/// <summary>
/// Owns one scheduler overview snapshot, authorization boundary, refresh loop, and async lifetime.
/// </summary>
public sealed class SchedulerOverviewPageState : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Res<JobSchedulerOverview>>> _loadOverview;
    private readonly Func<CancellationToken, Task<bool>> _authorize;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _refreshInterval;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private PeriodicTimer? _timer;
    private Task? _refreshLoop;
    private int _disposed;

    internal SchedulerOverviewPageState(
        Func<CancellationToken, Task<Res<JobSchedulerOverview>>> loadOverview,
        Func<CancellationToken, Task<bool>> authorize,
        TimeProvider timeProvider,
        TimeSpan refreshInterval)
    {
        _loadOverview = loadOverview;
        _authorize = authorize;
        _timeProvider = timeProvider;
        _refreshInterval = refreshInterval;
    }

    /// <summary>
    /// Raised after an accepted state transition.
    /// </summary>
    public event Func<Task>? Changed;

    /// <summary>
    /// Gets the latest bounded scheduler overview.
    /// </summary>
    public JobSchedulerOverview? Overview { get; private set; }

    /// <summary>
    /// Gets whether the first access check completed.
    /// </summary>
    public bool AccessChecked { get; private set; }

    /// <summary>
    /// Gets whether the current circuit is authorized.
    /// </summary>
    public bool IsAuthorized { get; private set; }

    /// <summary>
    /// Gets whether a refresh currently owns the facade boundary.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Gets the most recent load failure while preserving any prior snapshot.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets when the latest snapshot was accepted.
    /// </summary>
    public DateTimeOffset? LastRefreshedUtc { get; private set; }

    /// <summary>
    /// Performs the first access-checked load and starts automatic refresh when enabled.
    /// </summary>
    public async Task InitializeAsync()
    {
        ThrowIfDisposed();
        await RefreshAsync();
        if (_refreshInterval > TimeSpan.Zero && !IsDisposed)
        {
            _timer = new PeriodicTimer(_refreshInterval, _timeProvider);
            _refreshLoop = RunRefreshLoopAsync(_timer, _lifetimeCancellation.Token);
        }
    }

    /// <summary>
    /// Re-evaluates authorization and refreshes the operational snapshot.
    /// </summary>
    public async Task RefreshAsync()
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            IsLoading = true;
            await NotifyChangedAsync();

            IsAuthorized = await _authorize(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                Overview = null;
                Error = null;
                return;
            }

            var result = await _loadOverview(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var overview))
            {
                Error = error.Message;
                return;
            }

            Overview = overview;
            Error = null;
            LastRefreshedUtc = _timeProvider.GetUtcNow();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!IsDisposed)
            {
                IsLoading = false;
                await NotifyChangedAsync();
            }

            _refreshGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Changed = null;
        await _lifetimeCancellation.CancelAsync();
        _timer?.Dispose();
        if (_refreshLoop is not null)
        {
            await _refreshLoop;
        }

        await _refreshGate.WaitAsync();
        _refreshGate.Release();
        _refreshGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private async Task RunRefreshLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during page navigation or host shutdown.
        }
    }

    private async Task NotifyChangedAsync()
    {
        var handlers = Changed?.GetInvocationList().Cast<Func<Task>>().ToArray() ?? [];
        foreach (var handler in handlers)
        {
            await handler();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}
