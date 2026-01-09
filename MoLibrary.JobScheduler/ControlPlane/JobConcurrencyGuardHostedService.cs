using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Core;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.RegisterCentre.Events;
using MoLibrary.RegisterCentre.Interfaces;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Manages job concurrency limits by tracking running instances and listening to lifecycle events.
/// Extends CoordinatedLeaderService for consistent initialization with RegisterCentre coordination and leader-only execution.
/// Supports dynamic leader status changes - cleans up subscriptions on leader loss and re-initializes on leader gain.
/// </summary>
public class JobConcurrencyGuardHostedService(
    IJobDefinitionCacheService cacheService,
    IMoJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    JobInstanceManager instanceManager,
    ILogger<JobConcurrencyGuardHostedService> logger,
    ILeaderElectionService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions
) : CoordinatedLeaderService(leaderService, options, logger, coordinator, observableManager, hostedServiceOptions), IJobConcurrencyGuard
{
    private ConcurrentDictionary<string, JobExecutionStatistic> _statistics = new();
    private ConcurrentDictionary<string, SemaphoreSlim> _jobLocks = new();
    private List<IAsyncDisposable> _eventSubscriptions = [];

    public override string ServiceName => nameof(JobConcurrencyGuardHostedService);

    protected override async Task LeaderInitializeAsync(CancellationToken cancellationToken)
    {
        await InitializeConcurrencyTrackingAsync(cancellationToken);
    }

    private async Task InitializeConcurrencyTrackingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobConcurrencyGuard is initializing...");

        // 1. Load all job definitions from cache
        var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
        logger.LogDebug("Loaded {Count} job definitions", definitions.Count);

        // 2. Initialize statistics for each job definition
        foreach (var definition in definitions)
        {
            _statistics[definition.JobKey] = new JobExecutionStatistic
            {
                JobKey = definition.JobKey,
                MaxConcurrency = definition.MaxConcurrency,
                RunningInstances = []
            };

            _jobLocks[definition.JobKey] = new SemaphoreSlim(1, 1);
        }

        // 3. Scan all Enqueued and Processing state instances to recover in-memory state
        // Use single batch query instead of N queries (one per definition)
        var allJobKeys = definitions.Select(d => d.JobKey).ToList();
        logger.LogDebug("Querying instances for {Count} job definitions in batch", allJobKeys.Count);

        var batchQuery = new JobInstanceQuery
        {
            JobKeys = allJobKeys,
            States = [JobState.Enqueued, JobState.Processing],
            PageNumber = 1,
            PageSize = int.MaxValue
        };

        var batchResult = await metadataRepository.QueryInstancesAsync(batchQuery, cancellationToken);
        var allInstances = batchResult.Items;

        logger.LogDebug(
            "Retrieved {InstanceCount} active instances ({EnqueuedCount} Enqueued, {ProcessingCount} Processing)",
            allInstances.Count,
            allInstances.Count(i => i.State == JobState.Enqueued),
            allInstances.Count(i => i.State == JobState.Processing));

        // Group instances by JobKey for efficient recovery
        var instancesByJob = allInstances.GroupBy(i => i.JobKey);

        foreach (var group in instancesByJob)
        {
            var jobKey = group.Key;

            if (!_statistics.TryGetValue(jobKey, out var statistic))
            {
                logger.LogWarning(
                    "Found instances for unknown job {JobKey} during recovery, skipping",
                    jobKey);
                continue;
            }

            foreach (var instance in group)
            {
                if (instance.State == JobState.Enqueued)
                {
                    // Recover pending reservation
                    statistic.ReserveSlot(instance.InstanceId);

                    logger.LogDebug(
                        "Recovered pending reservation for instance {InstanceId} of job {JobKey}",
                        instance.InstanceId,
                        jobKey);
                }
                else if (instance.State == JobState.Processing)
                {
                    // Recover running instance
                    if (string.IsNullOrEmpty(instance.RunningClientId) || !instance.StartedAt.HasValue)
                    {
                        logger.LogWarning(
                            "Found Processing instance {InstanceId} with missing client ID or started time, skipping recovery",
                            instance.InstanceId);
                        continue;
                    }

                    statistic.AddInstance(new RunningJobInfo
                    {
                        InstanceId = instance.InstanceId,
                        WorkerClientId = instance.RunningClientId,
                        StartedAt = instance.StartedAt.Value
                    });

                    logger.LogDebug(
                        "Recovered running instance {InstanceId} for job {JobKey} on worker {WorkerId}",
                        instance.InstanceId,
                        jobKey,
                        instance.RunningClientId);
                }
            }
        }

        // 4. Subscribe to lifecycle events
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobStartedEvent>(OnJobStartedAsync));
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobCompletedEvent>(OnJobCompletedAsync));

        // 5. Subscribe to job definitions changed event to handle dynamically registered jobs
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobDefinitionsChangedEvent>(OnJobDefinitionsChangedAsync));

        // Note: Worker offline detection is now handled by the zombie detector service through timeout-based detection
        // The previous RegisterCentre server-based offline event mechanism has been removed in the StateStore refactoring

        logger.LogInformation(
            "JobConcurrencyGuard initialized with {DefinitionCount} job definitions, {PendingCount} pending reservations, and {RunningCount} running instances",
            _statistics.Count,
            _statistics.Values.Sum(s => s.PendingReservations.Count),
            _statistics.Values.Sum(s => s.RunningInstances.Count));
    }

    /// <summary>
    /// Cleans up event subscriptions and in-memory state when leader status is lost.
    /// This allows for proper re-initialization when leader status is re-gained.
    /// </summary>
    protected override async Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        logger.LogInformation("JobConcurrencyGuard cleaning up after losing leader status (reason: {Reason})", reason);

        // Unsubscribe from EventBus events
        foreach (var subscription in _eventSubscriptions)
        {
            await subscription.DisposeAsync();
        }
        _eventSubscriptions = [];

        // Dispose all semaphores
        foreach (var semaphore in _jobLocks.Values)
        {
            semaphore.Dispose();
        }

        // Reset state for potential re-initialization
        _jobLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        _statistics = new ConcurrentDictionary<string, JobExecutionStatistic>();

        logger.LogInformation("JobConcurrencyGuard cleanup completed");
    }

    public async Task<bool> CanExecuteJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (!_statistics.TryGetValue(jobKey, out var statistic))
        {
            logger.LogWarning("Job {JobKey} not found in concurrency guard statistics", jobKey);
            return false;
        }

        // Get job-specific lock to ensure thread-safety
        var semaphore = _jobLocks.GetOrAdd(jobKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var canExecute = statistic.CanExecute;

            if (!canExecute)
            {
                logger.LogWarning(
                    "Job {JobKey} exceeded MaxConcurrency limit: {Current}/{Max}. Running instances (up to 5): {RunningDetails}",
                    jobKey,
                    statistic.CurrentExecutingCount,
                    statistic.MaxConcurrency,
                    GetRunningInstancesSummary(statistic));
            }
            else
            {
                logger.LogDebug(
                    "Job {JobKey} can execute: {Current}/{Max}",
                    jobKey,
                    statistic.CurrentExecutingCount,
                    statistic.MaxConcurrency);
            }

            return canExecute;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<ReservationResult> TryReserveExecutionSlotAsync(
        string jobKey,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!_statistics.TryGetValue(jobKey, out var statistic))
        {
            var reason = $"Job {jobKey} not found in concurrency guard statistics";
            logger.LogWarning("{Reason}", reason);
            return ReservationResult.Failure(reason);
        }

        // Get job-specific lock to ensure thread-safety
        var semaphore = _jobLocks.GetOrAdd(jobKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (!statistic.CanExecute)
            {
                var runningDetails = GetRunningInstancesSummary(statistic);
                var reason = $"Job {jobKey} exceeded MaxConcurrency limit: {statistic.CurrentExecutingCount}/{statistic.MaxConcurrency}. Running instances (up to 5): {runningDetails}";
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} exceeded MaxConcurrency: {Current}/{Max}. Running instances (up to 5): {RunningDetails}",
                    jobKey,
                    instanceId,
                    statistic.CurrentExecutingCount,
                    statistic.MaxConcurrency,
                    runningDetails);
                return ReservationResult.Failure(reason);
            }

            // Reserve the slot
            statistic.ReserveSlot(instanceId);

            logger.LogDebug(
                "Reserved execution slot for job {JobKey} instance {InstanceId}: {Current}/{Max}",
                jobKey,
                instanceId,
                statistic.CurrentExecutingCount,
                statistic.MaxConcurrency);

            return ReservationResult.Success();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task ReleaseReservedSlotAsync(
        string jobKey,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!_statistics.TryGetValue(jobKey, out var statistic))
        {
            return;
        }

        var semaphore = _jobLocks.GetOrAdd(jobKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var (removedFromPending, removedFromRunning) = statistic.RemoveInstanceFromTracking(instanceId);
            if (removedFromPending || removedFromRunning)
            {
                logger.LogDebug(
                    "Released slot for job {JobKey} instance {InstanceId} (pending: {WasPending}, running: {WasRunning})",
                    jobKey,
                    instanceId,
                    removedFromPending,
                    removedFromRunning);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public Task<int> GetCurrentExecutingCountAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (_statistics.TryGetValue(jobKey, out var statistic))
        {
            return Task.FromResult(statistic.CurrentExecutingCount);
        }

        return Task.FromResult(0);
    }

    public Task<IReadOnlyDictionary<string, JobExecutionStatisticSnapshot>> GetAllExecutionStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = _statistics.ToDictionary(
            kvp => kvp.Key,
            kvp => new JobExecutionStatisticSnapshot
            {
                JobKey = kvp.Key,
                MaxConcurrency = kvp.Value.MaxConcurrency,
                RunningCount = kvp.Value.RunningInstances.Count,
                PendingCount = kvp.Value.PendingReservations.Count
            });

        return Task.FromResult<IReadOnlyDictionary<string, JobExecutionStatisticSnapshot>>(snapshots);
    }

    private async Task OnJobStartedAsync(JobStartedEvent evt)
    {
        if (!_statistics.TryGetValue(evt.JobKey, out var statistic))
        {
            logger.LogWarning(
                "Received JobStartedEvent for unknown job {JobKey}, instance {InstanceId}",
                evt.JobKey,
                evt.InstanceId);
            return;
        }

        var semaphore = _jobLocks.GetOrAdd(evt.JobKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            var info = new RunningJobInfo
            {
                InstanceId = evt.InstanceId,
                WorkerClientId = evt.WorkerClientId,
                StartedAt = evt.StartedAt
            };

            // Confirm reservation (move from pending to running)
            statistic.ConfirmReservation(evt.InstanceId, info);

            logger.LogDebug(
                "Job {JobKey} instance {InstanceId} started on worker {WorkerId}, current executing: {Current}/{Max}",
                evt.JobKey,
                evt.InstanceId,
                evt.WorkerClientId,
                statistic.CurrentExecutingCount,
                statistic.MaxConcurrency);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task OnJobCompletedAsync(JobCompletedEvent evt)
    {
        if (!_statistics.TryGetValue(evt.JobKey, out var statistic))
        {
            logger.LogWarning(
                "Received JobCompletedEvent for unknown job {JobKey}, instance {InstanceId}",
                evt.JobKey,
                evt.InstanceId);
            return;
        }

        var semaphore = _jobLocks.GetOrAdd(evt.JobKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            var (removedFromPending, removedFromRunning) = statistic.RemoveInstanceFromTracking(evt.InstanceId);

            if (removedFromPending || removedFromRunning)
            {
                logger.LogDebug(
                    "Job {JobKey} instance {InstanceId} completed with state {FinalState} and removed from tracking (pending: {WasPending}, running: {WasRunning}), current executing: {Current}/{Max}",
                    evt.JobKey,
                    evt.InstanceId,
                    evt.FinalState,
                    removedFromPending,
                    removedFromRunning,
                    statistic.CurrentExecutingCount,
                    statistic.MaxConcurrency);
            }
            else
            {
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} completed but was not found in tracking",
                    evt.JobKey,
                    evt.InstanceId);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Handles JobDefinitionsChangedEvent to add/update concurrency tracking for dynamically registered jobs.
    /// This resolves the race condition where JobConcurrencyGuardHostedService may initialize before
    /// JobRegistrationHostedService has saved job definitions to the repository.
    /// </summary>
    private Task OnJobDefinitionsChangedAsync(JobDefinitionsChangedEvent evt)
    {
        logger.LogDebug(
            "Received JobDefinitionsChangedEvent: {AddedCount} added, {UpdatedCount} updated, {DeletedCount} deleted",
            evt.AddedJobKeys.Count,
            evt.UpdatedJobKeys.Count,
            evt.DeletedJobKeys.Count);

        // 1. Add statistics for newly added jobs
        foreach (var definition in evt.AddedDefinitions)
        {
            if (!_statistics.ContainsKey(definition.JobKey))
            {
                _statistics[definition.JobKey] = new JobExecutionStatistic
                {
                    JobKey = definition.JobKey,
                    MaxConcurrency = definition.MaxConcurrency,
                    RunningInstances = []
                };
                _jobLocks[definition.JobKey] = new SemaphoreSlim(1, 1);

                logger.LogDebug(
                    "Added concurrency tracking for new job: {JobKey} (MaxConcurrency: {MaxConcurrency})",
                    definition.JobKey,
                    definition.MaxConcurrency);
            }
        }

        // 2. Handle updated jobs (update MaxConcurrency if changed)
        foreach (var definition in evt.UpdatedDefinitions)
        {
            if (_statistics.TryGetValue(definition.JobKey, out var statistic))
            {
                statistic.MaxConcurrency = definition.MaxConcurrency;
                logger.LogDebug(
                    "Updated MaxConcurrency for job {JobKey} to {MaxConcurrency}",
                    definition.JobKey,
                    definition.MaxConcurrency);
            }
        }

        // Note: We don't remove deleted jobs immediately to allow running instances to complete gracefully

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a summary of currently running instances for diagnostic logging.
    /// </summary>
    private static string GetRunningInstancesSummary(JobExecutionStatistic statistic)
    {
        var runningInfos = statistic.RunningInstances
            .Take(5)
            .Select(i => i.ToString());

        var pendingInfos = statistic.PendingReservations
            .Take(Math.Max(0, 5 - statistic.RunningInstances.Count))
            .Select(p => $"{p.Key}[Pending since {p.Value}]");

        var allInfos = runningInfos.Concat(pendingInfos).ToList();

        if (allInfos.Count == 0)
        {
            return "(none)";
        }

        return string.Join("\n", allInfos);
    }
}
