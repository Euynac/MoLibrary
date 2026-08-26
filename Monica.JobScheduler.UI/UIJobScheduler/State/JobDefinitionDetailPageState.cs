using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Analytics;
using Microsoft.Extensions.Localization;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.JobScheduler.UI.UIJobScheduler.Support;

namespace Monica.JobScheduler.UI.UIJobScheduler.State;

/// <summary>
/// Carries one bounded execution stream together with its authoritative filtered total and recoverable load error.
/// </summary>
public sealed record JobExecutionActivitySlice
{
    /// <summary>
    /// Gets an empty successful stream.
    /// </summary>
    public static JobExecutionActivitySlice Empty { get; } = new();

    /// <summary>
    /// Gets the maximum number of newest executions requested and rendered for this stream.
    /// </summary>
    public int ItemLimit { get; init; } = 5;

    /// <summary>
    /// Gets the newest bounded executions rendered by the detail page.
    /// </summary>
    public IReadOnlyList<JobExecutionInstance> Items { get; init; } = [];

    /// <summary>
    /// Gets the authoritative number of executions matching the stream query.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the latest recoverable query error while preserving previously accepted evidence.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Creates component-owned job-definition detail sessions from circuit-scoped dependencies.
/// </summary>
internal sealed class JobDefinitionDetailPageStateFactory(
    JobSchedulerFacade facade,
    IJobSchedulerUiAccess access,
    TimeProvider timeProvider,
    SchedulerTimePresentation timePresentation,
    IStringLocalizer<JobSchedulerResource> localizer)
{
    /// <summary>
    /// Creates a fresh detail state owned by one rendered route instance.
    /// </summary>
    public JobDefinitionDetailPageState Create(JobId jobId) =>
        new(facade, access, timeProvider, timePresentation, localizer, jobId);
}

/// <summary>
/// Owns one job-definition detail snapshot, authorization boundary, refresh serialization, and async lifetime.
/// </summary>
public sealed class JobDefinitionDetailPageState : IAsyncDisposable
{
    private readonly JobSchedulerFacade _facade;
    private readonly IJobSchedulerUiAccess _access;
    private readonly TimeProvider _timeProvider;
    private readonly SchedulerTimePresentation _timePresentation;
    private readonly IStringLocalizer<JobSchedulerResource> _localizer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private int _disposed;

    internal JobDefinitionDetailPageState(
        JobSchedulerFacade facade,
        IJobSchedulerUiAccess access,
        TimeProvider timeProvider,
        SchedulerTimePresentation timePresentation,
        IStringLocalizer<JobSchedulerResource> localizer,
        JobId jobId)
    {
        jobId.Validate();
        _facade = facade;
        _access = access;
        _timeProvider = timeProvider;
        _timePresentation = timePresentation;
        _localizer = localizer;
        JobId = jobId;
    }

    /// <summary>
    /// Raised after an accepted state transition.
    /// </summary>
    public event Func<Task>? Changed;

    /// <summary>
    /// Gets the exact definition identity represented by this state instance.
    /// </summary>
    public JobId JobId { get; }

    /// <summary>
    /// Gets the exact logical job key represented by this state instance.
    /// </summary>
    public string JobKey => JobId.JobKey;

    /// <summary>
    /// Gets the latest bounded operational projection.
    /// </summary>
    public JobOperationalSummary? Summary { get; private set; }

    /// <summary>
    /// Gets the latest 30-day bounded health projection for this job.
    /// </summary>
    public JobExecutionAnalyticsSnapshot? Analytics { get; private set; }

    /// <summary>
    /// Gets the newest bounded set of currently running executions for this logical job.
    /// </summary>
    public JobExecutionActivitySlice RunningActivity { get; private set; } = JobExecutionActivitySlice.Empty;

    /// <summary>
    /// Gets the newest bounded failed executions created within the health analytics window.
    /// </summary>
    public JobExecutionActivitySlice RecentFailureActivity { get; private set; } = JobExecutionActivitySlice.Empty;

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
    /// Gets whether an operator mutation currently owns the persistence boundary.
    /// </summary>
    public bool IsMutating { get; private set; }

    /// <summary>
    /// Gets the most recent load failure while preserving any prior snapshot.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets the latest analytics-only load failure without hiding the operational dossier.
    /// </summary>
    public string? AnalyticsError { get; private set; }

    /// <summary>
    /// Gets whether the authorized lookup completed without finding an active definition.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Gets when the current snapshot was accepted.
    /// </summary>
    public DateTimeOffset? ObservedAtUtc { get; private set; }

    /// <summary>
    /// Performs the first access-checked load.
    /// </summary>
    public Task InitializeAsync() => RefreshAsync();

    /// <summary>
    /// Re-evaluates authorization and replaces the operational snapshot when the read succeeds.
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

            IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                Summary = null;
                Analytics = null;
                RunningActivity = JobExecutionActivitySlice.Empty;
                RecentFailureActivity = JobExecutionActivitySlice.Empty;
                IsNotFound = false;
                Error = null;
                AnalyticsError = null;
                ObservedAtUtc = null;
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var summaryTask = _facade.GetOperationalSummaryAsync(JobId, cancellationToken);
            var analyticsQuery = SchedulerStatisticsPageState.CreateQuery(
                SchedulerAnalyticsTimeRange.Last30Days,
                now,
                _timePresentation,
                JobKey,
                JobId.OwnerKey);
            var analyticsTask = _facade.GetExecutionAnalyticsAsync(analyticsQuery, cancellationToken);
            var runningTask = QueryExecutionsAsync(
                JobExecutionState.Running,
                JobExecutionSortField.StartedAtUtc,
                null,
                null,
                RunningActivity.ItemLimit,
                cancellationToken);
            var failuresTask = QueryExecutionsAsync(
                JobExecutionState.Failed,
                JobExecutionSortField.CompletedAtUtc,
                analyticsQuery.StartTimeUtc,
                analyticsQuery.EndTimeUtc.AddTicks(-1),
                RecentFailureActivity.ItemLimit,
                cancellationToken);
            await Task.WhenAll(summaryTask, analyticsTask, runningTask, failuresTask);
            cancellationToken.ThrowIfCancellationRequested();
            var summaryResult = await summaryTask;
            if (summaryResult.IsFailed(out var error, out var summary))
            {
                Error = error.Message;
                return;
            }

            Summary = summary;
            IsNotFound = summary is null;
            Error = null;
            var analyticsResult = await analyticsTask;
            if (analyticsResult.IsFailed(out var analyticsError, out var analytics))
            {
                AnalyticsError = analyticsError.Message;
            }
            else
            {
                Analytics = analytics;
                AnalyticsError = null;
            }

            RunningActivity = AcceptExecutionEvidence(await runningTask, RunningActivity);
            RecentFailureActivity = AcceptExecutionEvidence(await failuresTask, RecentFailureActivity);

            ObservedAtUtc = now;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during route changes, page navigation, or host shutdown.
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
    /// Reauthorizes the circuit and replaces only the disabled policy override for the current active definition.
    /// </summary>
    public async Task<Res<JobPolicy>> SetDisabledAsync(bool disabled)
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            IsMutating = true;
            await NotifyChangedAsync();

            IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                return _localizer["Access:DeniedDescription"].Value;
            }

            if (Summary is not { } summary)
            {
                return _localizer["JobDetail:NotFound", JobKey].Value;
            }

            if (disabled && JobSchedulerUiPresentation.IsDebugOnlySuppressed(summary))
            {
                return _localizer["Catalog:Messages:PauseUnavailableDebug"].Value;
            }

            var definition = summary.Definition;
            var result = await _facade.UpdatePolicyAsync(
                definition.OwnerKey,
                definition.Declaration.JobKey,
                new JobPolicyChange
                {
                    Overrides = definition.Policy.Overrides with { DisabledOverride = disabled },
                    ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
                },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.IsFailed(out _, out var policy))
            {
                var suspensionReasons = JobSchedulerUiPresentation.SetOperatorPolicySuspension(
                    summary.SuspensionReasons,
                    disabled);
                Summary = summary with
                {
                    Definition = definition with { Policy = policy },
                    RecurringScheduleStatus = suspensionReasons != JobRecurringScheduleSuspensionReason.None
                        ? JobRecurringScheduleStatus.Suspended
                        : JobRecurringScheduleStatus.AwaitingSynchronization,
                    SuspensionReasons = suspensionReasons,
                    NextOccurrenceUtc = null
                };
                ObservedAtUtc = _timeProvider.GetUtcNow();
            }

            return result;
        }
        finally
        {
            if (!IsDisposed)
            {
                IsMutating = false;
                await NotifyChangedAsync();
            }

            _mutationGate.Release();
        }
    }

    /// <summary>
    /// Reauthorizes the circuit and admits an immediate operator occurrence without advancing the recurring cursor.
    /// </summary>
    public async Task<Res<JobExecutionInstance>> RunRecurringNowAsync()
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            IsMutating = true;
            await NotifyChangedAsync();

            IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                return _localizer["Access:DeniedDescription"].Value;
            }

            if (Summary is not { } summary)
            {
                return _localizer["JobDetail:NotFound", JobKey].Value;
            }

            var definition = summary.Definition;
            var result = await _facade.RunRecurringNowAsync(
                new JobRecurringRunNowRequest
                {
                    OwnerKey = definition.OwnerKey,
                    JobKey = definition.Declaration.JobKey
                },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.IsFailed(out _, out var execution))
            {
                Summary = summary with
                {
                    LatestExecution = execution,
                    QueuedExecutionCount = summary.QueuedExecutionCount + 1
                };
                ObservedAtUtc = _timeProvider.GetUtcNow();
            }

            return result;
        }
        finally
        {
            if (!IsDisposed)
            {
                IsMutating = false;
                await NotifyChangedAsync();
            }

            _mutationGate.Release();
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
        await _refreshGate.WaitAsync();
        _refreshGate.Release();
        await _mutationGate.WaitAsync();
        _mutationGate.Release();
        _refreshGate.Dispose();
        _mutationGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private Task<Res<QueryResult<JobExecutionInstance>>> QueryExecutionsAsync(
        JobExecutionState state,
        JobExecutionSortField sortField,
        DateTimeOffset? createdAfterUtc,
        DateTimeOffset? createdBeforeUtc,
        int itemLimit,
        CancellationToken cancellationToken) =>
        _facade.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = string.Empty,
            OwnerKey = JobId.OwnerKey,
            JobKey = JobKey,
            States = [state],
            CreatedAfterUtc = createdAfterUtc,
            CreatedBeforeUtc = createdBeforeUtc,
            SortField = sortField,
            SortDescending = true,
            PageNumber = 1,
            PageSize = itemLimit
        }, cancellationToken);

    private static JobExecutionActivitySlice AcceptExecutionEvidence(
        Res<QueryResult<JobExecutionInstance>> result,
        JobExecutionActivitySlice current)
    {
        if (result.IsFailed(out var error, out var page))
        {
            return current with { Error = error.Message };
        }

        return new JobExecutionActivitySlice
        {
            Items = page.Items,
            TotalCount = page.TotalCount,
            ItemLimit = current.ItemLimit
        };
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
