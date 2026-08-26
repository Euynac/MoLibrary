using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.JobScheduler.UI.UIJobScheduler.Support;

namespace Monica.JobScheduler.UI.UIJobScheduler.State;

/// <summary>
/// Defines the bounded ranges available from the scheduler statistics workspace.
/// </summary>
public enum SchedulerAnalyticsTimeRange
{
    /// <summary>
    /// Includes the current calendar day in the configured scheduler timezone.
    /// </summary>
    Today,

    /// <summary>
    /// Includes the rolling previous 24 hours.
    /// </summary>
    Last24Hours,

    /// <summary>
    /// Includes the rolling previous seven days.
    /// </summary>
    Last7Days,

    /// <summary>
    /// Includes the rolling previous 30 days.
    /// </summary>
    Last30Days
}

/// <summary>
/// Creates component-owned statistics sessions from circuit-scoped dependencies.
/// </summary>
internal sealed class SchedulerStatisticsPageStateFactory(
    JobSchedulerFacade facade,
    IJobSchedulerUiAccess access,
    TimeProvider timeProvider,
    SchedulerTimePresentation timePresentation)
{
    /// <summary>
    /// Creates a fresh cross-job analytics session.
    /// </summary>
    public SchedulerStatisticsPageState Create() => new(facade, access, timeProvider, timePresentation);
}

/// <summary>
/// Owns authorization, range selection, bounded analytics loading, and async lifetime for the statistics route.
/// </summary>
public sealed class SchedulerStatisticsPageState : IAsyncDisposable
{
    private readonly JobSchedulerFacade _facade;
    private readonly IJobSchedulerUiAccess _access;
    private readonly TimeProvider _timeProvider;
    private readonly SchedulerTimePresentation _timePresentation;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _disposed;

    internal SchedulerStatisticsPageState(
        JobSchedulerFacade facade,
        IJobSchedulerUiAccess access,
        TimeProvider timeProvider,
        SchedulerTimePresentation timePresentation)
    {
        _facade = facade;
        _access = access;
        _timeProvider = timeProvider;
        _timePresentation = timePresentation;
    }

    /// <summary>
    /// Raised after an accepted state transition.
    /// </summary>
    public event Func<Task>? Changed;

    /// <summary>
    /// Gets the selected bounded time range.
    /// </summary>
    public SchedulerAnalyticsTimeRange TimeRange { get; private set; } = SchedulerAnalyticsTimeRange.Last24Hours;

    /// <summary>
    /// Gets the latest analytics snapshot.
    /// </summary>
    public JobExecutionAnalyticsSnapshot? Snapshot { get; private set; }

    /// <summary>
    /// Gets whether authorization has completed.
    /// </summary>
    public bool AccessChecked { get; private set; }

    /// <summary>
    /// Gets whether the current circuit may inspect scheduler data.
    /// </summary>
    public bool IsAuthorized { get; private set; }

    /// <summary>
    /// Gets whether analytics are currently loading.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Gets the most recent load failure while preserving a prior snapshot.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets when the latest snapshot was accepted.
    /// </summary>
    public DateTimeOffset? ObservedAtUtc { get; private set; }

    /// <summary>
    /// Performs the first access-checked analytics load.
    /// </summary>
    public Task InitializeAsync() => RefreshAsync();

    /// <summary>
    /// Selects a preset and immediately reloads its bounded analytics.
    /// </summary>
    public async Task SetTimeRangeAsync(SchedulerAnalyticsTimeRange value)
    {
        if (value == TimeRange)
        {
            return;
        }

        await RefreshAsync(value);
    }

    /// <summary>
    /// Reauthorizes and replaces the selected analytics snapshot.
    /// </summary>
    public Task RefreshAsync() => RefreshAsync(TimeRange);

    private async Task RefreshAsync(SchedulerAnalyticsTimeRange requestedRange)
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            IsLoading = true;
            await NotifyChangedAsync();

            IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                Snapshot = null;
                Error = null;
                ObservedAtUtc = null;
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var result = await _facade.GetExecutionAnalyticsAsync(
                CreateQuery(requestedRange, now, _timePresentation),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var snapshot))
            {
                Error = error.Message;
                return;
            }

            Snapshot = snapshot;
            TimeRange = requestedRange;
            Error = null;
            ObservedAtUtc = now;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during navigation or host shutdown.
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

    /// <summary>
    /// Creates the server-bounded analytics query for one UI preset.
    /// </summary>
    internal static JobExecutionAnalyticsQuery CreateQuery(
        SchedulerAnalyticsTimeRange range,
        DateTimeOffset now,
        SchedulerTimePresentation timePresentation,
        string? jobKey = null,
        string? ownerKey = null)
    {
        ArgumentNullException.ThrowIfNull(timePresentation);
        var end = now.ToUniversalTime();
        var (start, bucketSize) = range switch
        {
            SchedulerAnalyticsTimeRange.Today =>
                (timePresentation.GetStartOfSchedulerDayUtc(end), JobExecutionAnalyticsBucketSize.Hour),
            SchedulerAnalyticsTimeRange.Last24Hours =>
                (end.AddHours(-24), JobExecutionAnalyticsBucketSize.Hour),
            SchedulerAnalyticsTimeRange.Last7Days =>
                (end.AddDays(-7), JobExecutionAnalyticsBucketSize.Day),
            SchedulerAnalyticsTimeRange.Last30Days =>
                (end.AddDays(-30), JobExecutionAnalyticsBucketSize.Day),
            _ => throw new ArgumentOutOfRangeException(nameof(range), range, "Analytics time range is not supported.")
        };

        if (start >= end)
        {
            end = start.AddTicks(1);
        }

        return new JobExecutionAnalyticsQuery
        {
            StartTimeUtc = start,
            EndTimeUtc = end,
            BucketSize = bucketSize,
            OwnerKey = ownerKey,
            JobKey = jobKey,
            TopJobLimit = 10,
            SlowestExecutionLimit = 10
        };
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
        await _refreshGate.WaitAsync();
        _refreshGate.Release();
        _refreshGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

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
