using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.Modules;

namespace Monica.JobScheduler.Facades;

/// <summary>
/// Exposes the durable scheduler control surface to host APIs and the JobScheduler UI.
/// </summary>
/// <remarks>
/// The facade never reconstructs scheduler state from events or caches. Every operation delegates to the unified
/// scheduler store, which owns admission, catalog, policy, execution, lease, and cancellation atomicity.
/// </remarks>
public sealed class JobSchedulerFacade(
    IJobSchedulerStore store,
    IOptions<ModuleJobSchedulerOption> options,
    TimeProvider timeProvider,
    ILogger<JobSchedulerFacade> logger)
{
    private const int MAX_PAGE_SIZE = 200;
    private const int RECENT_EXECUTION_COUNT = 12;

    private readonly string _schedulerScopeKey = options.Value.SchedulerScopeKey;

    /// <summary>
    /// Captures desired catalog publication, active catalog, worker convergence, and queue activity.
    /// </summary>
    /// <param name="cancellationToken">Cancels snapshot loading.</param>
    /// <returns>A result containing one bounded operational snapshot.</returns>
    public Task<Res<JobSchedulerOverview>> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var publicationTask = store.GetCatalogPublicationStatusAsync(_schedulerScopeKey, cancellationToken);
            var catalogTask = store.GetActiveCatalogAsync(_schedulerScopeKey, cancellationToken);
            var statisticsTask = store.GetExecutionStateStatisticsAsync(
                _schedulerScopeKey,
                cancellationToken: cancellationToken);
            var workersTask = store.GetActiveWorkerCapabilitiesAsync(
                _schedulerScopeKey,
                cancellationToken: cancellationToken);
            var recentTask = store.QueryExecutionsAsync(
                new JobExecutionQuery
                {
                    SchedulerScopeKey = _schedulerScopeKey,
                    PageNumber = 1,
                    PageSize = RECENT_EXECUTION_COUNT,
                    SortField = JobExecutionSortField.CreatedAtUtc,
                    SortDescending = true
                },
                cancellationToken);

            await Task.WhenAll(publicationTask, catalogTask, statisticsTask, workersTask, recentTask);
            var publication = await publicationTask;
            var activeCatalog = await catalogTask;
            var activeWorkers = await workersTask;
            var owners = publication.Owners.Select(owner => new JobOwnerConvergence
            {
                OwnerId = owner.OwnerId,
                WorkerRevisionId = owner.WorkerRevisionId,
                HasPublishedSnapshot = owner.IsPublished,
                ActiveWorkerCount = activeWorkers.Count(worker =>
                    string.Equals(worker.Capability.OwnerKey, owner.OwnerId, StringComparison.Ordinal)
                    && string.Equals(
                        worker.Capability.WorkerRevisionId,
                        owner.WorkerRevisionId,
                        StringComparison.Ordinal)),
                ActiveDefinitionCount = activeCatalog?.Definitions.Count(definition =>
                    string.Equals(definition.OwnerId, owner.OwnerId, StringComparison.Ordinal)
                    && string.Equals(
                        definition.WorkerRevisionId,
                        owner.WorkerRevisionId,
                        StringComparison.Ordinal)) ?? 0
            }).ToArray();

            return new JobSchedulerOverview
            {
                CatalogPublication = publication,
                ActiveCatalog = activeCatalog,
                ExecutionStateCounts = await statisticsTask,
                Owners = owners,
                RecentExecutions = (await recentTask).Items,
                CapturedAtUtc = timeProvider.GetUtcNow()
            };
        }, "load the scheduler overview", cancellationToken);

    /// <summary>
    /// Queries immutable active definitions together with their independent operator policies.
    /// </summary>
    public Task<Res<QueryResult<ActiveJobDefinition>>> QueryDefinitionsAsync(
        JobCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var bounded = query with
        {
            PageNumber = Math.Max(1, query.PageNumber),
            PageSize = Math.Clamp(query.PageSize, 1, MAX_PAGE_SIZE)
        };
        return ExecuteAsync(
            () => store.QueryActiveDefinitionsAsync(_schedulerScopeKey, bounded, cancellationToken),
            "query active job definitions",
            cancellationToken);
    }

    /// <summary>
    /// Gets one active definition by its globally unique logical job key.
    /// </summary>
    public Task<Res<ActiveJobDefinition?>> GetDefinitionAsync(
        string jobKey,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () => store.GetActiveDefinitionAsync(_schedulerScopeKey, jobKey, cancellationToken),
            "load an active job definition",
            cancellationToken);

    /// <summary>
    /// Replaces only operator-owned policy fields using optimistic concurrency.
    /// </summary>
    public Task<Res<JobPolicy>> UpdatePolicyAsync(
        string ownerId,
        string jobKey,
        JobPolicyChange change,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () => store.UpdatePolicyAsync(_schedulerScopeKey, ownerId, jobKey, change, cancellationToken),
            "update job policy",
            cancellationToken);

    /// <summary>
    /// Admits one execution through the active catalog and durable queue.
    /// </summary>
    public Task<Res<JobExecutionInstance>> TriggerAsync(
        JobTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var enqueueRequest = new JobEnqueueRequest
        {
            SchedulerScopeKey = _schedulerScopeKey,
            InstanceId = string.IsNullOrWhiteSpace(request.InstanceId)
                ? Guid.NewGuid().ToString("N")
                : request.InstanceId,
            JobKey = request.JobKey,
            ExpectedOwnerId = request.ExpectedOwnerId,
            ExpectedJobRevisionId = request.ExpectedJobRevisionId,
            JobArgs = request.JobArgs,
            AvailableAtUtc = request.AvailableAtUtc,
            EnqueueReason = "Operator-triggered execution admitted through the active catalog"
        };
        return ExecuteAsync(
            () => store.EnqueueAsync(enqueueRequest, cancellationToken),
            "trigger a job execution",
            cancellationToken);
    }

    /// <summary>
    /// Queries durable executions through a bounded page. The configured scheduler scope always overrides caller data.
    /// </summary>
    public Task<Res<QueryResult<JobExecutionInstance>>> QueryExecutionsAsync(
        JobExecutionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var bounded = query with
        {
            SchedulerScopeKey = _schedulerScopeKey,
            PageNumber = Math.Max(1, query.PageNumber),
            PageSize = Math.Clamp(query.PageSize, 1, MAX_PAGE_SIZE)
        };
        return ExecuteAsync(
            () => store.QueryExecutionsAsync(bounded, cancellationToken),
            "query job executions",
            cancellationToken);
    }

    /// <summary>
    /// Gets one durable execution including its append-only history.
    /// </summary>
    public Task<Res<JobExecutionInstance?>> GetExecutionAsync(
        string instanceId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () => store.GetExecutionAsync(_schedulerScopeKey, instanceId, cancellationToken),
            "load a job execution",
            cancellationToken);

    /// <summary>
    /// Cancels queued work immediately or records a cooperative request for the current fenced worker.
    /// </summary>
    public Task<Res<JobCancellationResult>> CancelExecutionAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () => store.RequestCancellationAsync(
                _schedulerScopeKey,
                instanceId,
                reason ?? "Cancellation requested by an operator",
                cancellationToken),
            "cancel a job execution",
            cancellationToken);

    /// <summary>
    /// Explicitly selects a fully published immutable release as a new activation intent.
    /// </summary>
    /// <remarks>
    /// Normal deployments use monotonic deployment generation. This operation exists for deliberate operator
    /// rollback and appends a new activation epoch rather than mutating history.
    /// </remarks>
    public Task<Res<JobCatalogActivation>> ReactivateReleaseAsync(
        string releaseId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () => store.ReactivateReleaseAsync(_schedulerScopeKey, releaseId, cancellationToken),
            "reactivate a catalog release",
            cancellationToken);

    private async Task<Res<T>> ExecuteAsync<T>(
        Func<Task<T>> action,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return Res.Ok(await action());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to {SchedulerOperation}.", operation);
            return Res.Fail($"Failed to {operation}: {exception.GetMessageRecursively()}");
        }
    }
}
