using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.UIJobScheduler.Abstractions;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;

namespace Monica.JobScheduler.UI.UIJobScheduler.State;

/// <summary>
/// Creates component-owned scheduler runtime sessions from circuit-scoped dependencies.
/// </summary>
internal sealed class SchedulerRuntimePageStateFactory(
    JobSchedulerFacade facade,
    IJobSchedulerUiAccess access,
    TimeProvider timeProvider,
    IServiceProvider serviceProvider)
{
    /// <summary>
    /// Creates a fresh runtime state owned by one rendered page. Worker-instance insight is attached only when an
    /// integration provider is registered; otherwise the page degrades to its setup hint.
    /// </summary>
    public SchedulerRuntimePageState Create(TimeSpan refreshInterval) =>
        new(
            facade.GetRuntimeOverviewAsync,
            access.IsAuthorizedAsync,
            timeProvider,
            refreshInterval,
            serviceProvider.GetService<IJobSchedulerWorkerInsightProvider>());
}

/// <summary>
/// Owns one scheduler runtime snapshot, its optional worker-instance insight, authorization boundary, refresh
/// loop, and async lifetime.
/// </summary>
public sealed class SchedulerRuntimePageState : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Res<JobSchedulerRuntimeOverview>>> _loadOverview;
    private readonly Func<CancellationToken, Task<bool>> _authorize;
    private readonly IJobSchedulerWorkerInsightProvider? _workerInsight;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _refreshInterval;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private PeriodicTimer? _timer;
    private Task? _refreshLoop;
    private int _disposed;

    internal SchedulerRuntimePageState(
        Func<CancellationToken, Task<Res<JobSchedulerRuntimeOverview>>> loadOverview,
        Func<CancellationToken, Task<bool>> authorize,
        TimeProvider timeProvider,
        TimeSpan refreshInterval,
        IJobSchedulerWorkerInsightProvider? workerInsight)
    {
        _loadOverview = loadOverview;
        _authorize = authorize;
        _timeProvider = timeProvider;
        _refreshInterval = refreshInterval;
        _workerInsight = workerInsight;
    }

    /// <summary>
    /// Raised after an accepted state transition.
    /// </summary>
    public event Func<Task>? Changed;

    /// <summary>
    /// Gets the latest host-scoped runtime snapshot.
    /// </summary>
    public JobSchedulerRuntimeOverview? Overview { get; private set; }

    /// <summary>
    /// Gets the latest worker-instance insight; empty until a provider-backed refresh succeeds.
    /// </summary>
    public IReadOnlyList<JobSchedulerWorkerInstanceView> Workers { get; private set; } = [];

    /// <summary>
    /// Gets whether a worker-instance insight provider is registered for this host.
    /// </summary>
    public bool HasWorkerInsight => _workerInsight is not null;

    /// <summary>
    /// Gets the most recent worker-insight load failure while preserving any prior instance list.
    /// </summary>
    public string? WorkerInsightError { get; private set; }

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
    /// Gets the most recent runtime load failure while preserving any prior snapshot.
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
    /// Re-evaluates authorization and refreshes the runtime snapshot and optional worker insight.
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
                Workers = [];
                WorkerInsightError = null;
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
            await RefreshWorkerInsightAsync(cancellationToken);
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

    // Worker insight is an optional enrichment: its failure degrades only its own panel and never the runtime
    // snapshot that the rest of the page renders.
    private async Task RefreshWorkerInsightAsync(CancellationToken cancellationToken)
    {
        if (_workerInsight is null)
        {
            return;
        }

        try
        {
            Workers = await _workerInsight.GetWorkerInstancesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            WorkerInsightError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WorkerInsightError = exception.Message;
        }
    }

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
