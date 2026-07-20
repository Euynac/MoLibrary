using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Services.Support;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Manages job concurrency limits by tracking running instances and listening to lifecycle events.
/// Extends CoordinatedLeaderService for consistent initialization with service registration coordination and leader-only execution.
/// Supports dynamic leader status changes - cleans up subscriptions on leader loss and re-initializes on leader gain.
/// </summary>
public class JobConcurrencyGuardHostedService(
    IJobDefinitionCacheService cacheService,
    IJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    IMoHostedServiceCheckpointCoordinator hostedServiceCheckpointCoordinator,
    ILeaderElectionService leaderService,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions,
    IOptions<ModuleJobSchedulerOption> jobSchedulerOptions,
    ILogger<JobConcurrencyGuardHostedService> logger
) : CoordinatedLeaderService(leaderService, serviceDiscoveryOptions, coordinator, observableManager, hostedServiceOptions, logger), IJobConcurrencyGuard
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = jobSchedulerOptions.Value;
    private ConcurrentDictionary<string, JobExecutionStatistic> _statistics = new();
    private ConcurrentDictionary<string, SemaphoreSlim> _jobLocks = new();
    private List<IAsyncDisposable> _eventSubscriptions = [];

    public override string ServiceName => nameof(JobConcurrencyGuardHostedService);
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.JobScheduler);

    protected override async Task OnBecameLeaderAsync(CancellationToken cancellationToken)
    {
        RecordState(
            $"Waiting for {nameof(JobRegistrationHostedService)} checkpoint '{JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady}'",
            HostedServiceState.WaitingDependency,
            logLevel: LogLevel.Information);

        await hostedServiceCheckpointCoordinator.WaitForCheckpointAsync<JobRegistrationHostedService>(
            JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady,
            LeaderService.LeaderBecomeTime,
            cancellationToken);

        RecordState(
            $"{nameof(JobRegistrationHostedService)} checkpoint '{JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady}' reached, continuing initialization",
            HostedServiceState.Executing,
            logLevel: LogLevel.Information);

        await InitializeConcurrencyTrackingAsync(cancellationToken);
    }

    private async Task InitializeConcurrencyTrackingAsync(CancellationToken cancellationToken)
    {
        RecordState("JobConcurrencyGuard is initializing...", logLevel: LogLevel.Information);

        // 1. Load all job definitions from cache
        var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
        
        if (definitions.Count == 0)
        {
            RecordState(
                "No job definitions found during initialization. Subscribing to events and waiting for future job definitions.",
                logLevel: LogLevel.Warning);

            await SubscribeLifecycleEventsAsync();

            RecordState(
                "JobConcurrencyGuard initialized with 0 job definitions and 0 active instances",
                logLevel: LogLevel.Information);
            return;
        }

        
        RecordState($"Loaded {definitions.Count} job definitions", logLevel: LogLevel.Debug);

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
        RecordState($"Querying instances for {allJobKeys.Count} job definitions in batch", logLevel: LogLevel.Debug);

        var batchQuery = new JobInstanceQuery
        {
            JobKeys = allJobKeys,
            States = [JobState.Enqueued, JobState.Processing],
            PageNumber = 1,
            PageSize = int.MaxValue
        };

        var batchResult = await metadataRepository.QueryInstancesAsync(batchQuery, cancellationToken);
        var allInstances = batchResult.Items;

        RecordState(
            $"Retrieved {allInstances.Count} active instances ({allInstances.Count(i => i.State == JobState.Enqueued)} Enqueued, {allInstances.Count(i => i.State == JobState.Processing)} Processing)",
            logLevel: LogLevel.Debug);

        // Group instances by JobKey for efficient recovery
        var instancesByJob = allInstances.GroupBy(i => i.JobKey);

        foreach (var group in instancesByJob)
        {
            var jobKey = group.Key;

            if (!_statistics.TryGetValue(jobKey, out var statistic))
            {
                RecordState(
                    $"Found instances for unknown job {jobKey} during recovery, skipping",
                    logLevel: LogLevel.Warning);
                continue;
            }

            foreach (var instance in group)
            {
                if (instance.State == JobState.Enqueued)
                {
                    // Recover pending reservation
                    statistic.ReserveSlot(instance.InstanceId);

                    RecordState(
                        $"Recovered pending reservation for instance {instance.InstanceId} of job {jobKey}",
                        logLevel: LogLevel.Debug);
                }
                else if (instance.State == JobState.Processing)
                {
                    // Recover running instance
                    if (string.IsNullOrEmpty(instance.RunningClientId) || !instance.StartedAt.HasValue)
                    {
                        RecordState(
                            $"Found Processing instance {instance.InstanceId} with missing client ID or started time, skipping recovery",
                            logLevel: LogLevel.Warning);
                        continue;
                    }

                    statistic.AddInstance(new RunningJobInfo
                    {
                        InstanceId = instance.InstanceId,
                        WorkerClientId = instance.RunningClientId,
                        StartedAt = instance.StartedAt.Value
                    });

                    RecordState(
                        $"Recovered running instance {instance.InstanceId} for job {jobKey} on worker {instance.RunningClientId}",
                        logLevel: LogLevel.Debug);
                }
            }
        }

        // 4. Subscribe to lifecycle events
        await SubscribeLifecycleEventsAsync();

        // Note: Worker offline detection is now handled by the zombie detector service through timeout-based detection
        // The previous registry-server-based offline event mechanism was removed during the state store refactoring.

        RecordState(
            $"JobConcurrencyGuard initialized with {_statistics.Count} job definitions, {_statistics.Values.Sum(s => s.PendingReservations.Count)} pending reservations, and {_statistics.Values.Sum(s => s.RunningInstances.Count)} running instances",
            logLevel: LogLevel.Information);
    }

    private async Task SubscribeLifecycleEventsAsync()
    {
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobStartedEvent>(
            OnJobStartedAsync,
            JobEventTopicHelper.GetTopicName<JobStartedEvent>(_jobSchedulerOptions.SchedulerScopeKey)));
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobCompletedEvent>(
            OnJobCompletedAsync,
            JobEventTopicHelper.GetTopicName<JobCompletedEvent>(_jobSchedulerOptions.SchedulerScopeKey)));
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobDefinitionsChangedEvent>(
            OnJobDefinitionsChangedAsync,
            JobEventTopicHelper.GetTopicName<JobDefinitionsChangedEvent>(_jobSchedulerOptions.SchedulerScopeKey)));
    }

    /// <summary>
    /// Cleans up event subscriptions and in-memory state when leader status is lost.
    /// This allows for proper re-initialization when leader status is re-gained.
    /// </summary>
    protected override async Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        RecordState($"JobConcurrencyGuard cleaning up after losing leader status (reason: {reason})", logLevel: LogLevel.Information);

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

        RecordState("JobConcurrencyGuard cleanup completed", logLevel: LogLevel.Information);
    }

    public async Task<bool> CanExecuteJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (!_statistics.TryGetValue(jobKey, out var statistic))
        {
            RecordState($"Job {jobKey} not found in concurrency guard statistics", logLevel: LogLevel.Warning);
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
                RecordState(
                    $"Job {jobKey} exceeded MaxConcurrency limit: {statistic.CurrentExecutingCount}/{statistic.MaxConcurrency}. Running instances (up to 5): {GetRunningInstancesSummary(statistic)}",
                    logLevel: LogLevel.Warning);
            }
            else
            {
                RecordState(
                    $"Job {jobKey} can execute: {statistic.CurrentExecutingCount}/{statistic.MaxConcurrency}",
                    logLevel: LogLevel.Debug);
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
            RecordState(reason, logLevel: LogLevel.Warning);
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
                RecordState(
                    $"Job {jobKey} instance {instanceId} exceeded MaxConcurrency: {statistic.CurrentExecutingCount}/{statistic.MaxConcurrency}. Running instances (up to 5): {runningDetails}",
                    logLevel: LogLevel.Warning);
                return ReservationResult.Failure(reason);
            }

            // Reserve the slot
            statistic.ReserveSlot(instanceId);

            RecordState(
                $"Reserved execution slot for job {jobKey} instance {instanceId}: {statistic.CurrentExecutingCount}/{statistic.MaxConcurrency}",
                logLevel: LogLevel.Debug);

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
                RecordState(
                    $"Released slot for job {jobKey} instance {instanceId} (pending: {removedFromPending}, running: {removedFromRunning})",
                    logLevel: LogLevel.Debug);
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

    public Task<IReadOnlyDictionary<string, JobExecutionStatistic>> GetDetailedExecutionStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        // Return a shallow copy of statistics to prevent external modification
        var copy = _statistics.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);

        return Task.FromResult<IReadOnlyDictionary<string, JobExecutionStatistic>>(copy);
    }

    public async Task<ConsistencyCheckResult> CheckConsistencyAsync(CancellationToken cancellationToken = default)
    {
        // 1. Get current in-memory state snapshot
        var memoryState = await GetAllExecutionStatisticsAsync(cancellationToken);

        // 2. Query database for actual Processing and Enqueued counts per job
        var allJobKeys = memoryState.Keys.ToList();

        if (allJobKeys.Count == 0)
        {
            return new ConsistencyCheckResult
            {
                TotalDeviation = 0,
                Deviations = []
            };
        }

        var batchQuery = new JobInstanceQuery
        {
            JobKeys = allJobKeys,
            States = [JobState.Enqueued, JobState.Processing],
            PageNumber = 1,
            PageSize = int.MaxValue
        };

        var dbResult = await metadataRepository.QueryInstancesAsync(batchQuery, cancellationToken);

        // Group by JobKey and State
        var dbCounts = dbResult.Items
            .GroupBy(i => i.JobKey)
            .ToDictionary(
                g => g.Key,
                g => (
                    Processing: g.Count(i => i.State == JobState.Processing),
                    Enqueued: g.Count(i => i.State == JobState.Enqueued)
                ));

        // 3. Compare and build deviations
        var deviations = new List<JobConsistencyDeviation>();

        foreach (var kvp in memoryState)
        {
            var jobKey = kvp.Key;
            var memoryStats = kvp.Value;
            var dbStats = dbCounts.GetValueOrDefault(jobKey, (Processing: 0, Enqueued: 0));

            var deviation = new JobConsistencyDeviation
            {
                JobKey = jobKey,
                MemoryRunningCount = memoryStats.RunningCount,
                DatabaseProcessingCount = dbStats.Processing,
                MemoryPendingCount = memoryStats.PendingCount,
                DatabaseEnqueuedCount = dbStats.Enqueued
            };

            if (deviation.HasDeviation)
            {
                deviations.Add(deviation);
            }
        }

        // Also check for jobs in DB but not in memory
        foreach (var jobKey in dbCounts.Keys.Except(memoryState.Keys))
        {
            var dbStats = dbCounts[jobKey];
            deviations.Add(new JobConsistencyDeviation
            {
                JobKey = jobKey,
                MemoryRunningCount = 0,
                DatabaseProcessingCount = dbStats.Processing,
                MemoryPendingCount = 0,
                DatabaseEnqueuedCount = dbStats.Enqueued
            });
        }

        return new ConsistencyCheckResult
        {
            TotalDeviation = deviations.Sum(d => Math.Abs(d.RunningDeviation) + Math.Abs(d.PendingDeviation)),
            Deviations = deviations,
            CheckedAt = DateTime.UtcNow
        };
    }

    public async Task<ReconcileResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. Check state before reconciliation
            var stateBefore = await CheckConsistencyAsync(cancellationToken);

            RecordState($"Starting reconciliation. Current deviation: {stateBefore.TotalDeviation}", logLevel: LogLevel.Information);

            // 2. Acquire all job locks to prevent concurrent modifications
            var allLocks = _jobLocks.Values.ToList();
            foreach (var semaphore in allLocks)
            {
                await semaphore.WaitAsync(cancellationToken);
            }

            try
            {
                // 3. Clear current in-memory state (but preserve MaxConcurrency settings)
                foreach (var stat in _statistics.Values)
                {
                    stat.RunningInstances.Clear();
                    stat.PendingReservations.Clear();
                }

                // 4. Reload from database
                var allJobKeys = _statistics.Keys.ToList();

                if (allJobKeys.Count > 0)
                {
                    var batchQuery = new JobInstanceQuery
                    {
                        JobKeys = allJobKeys,
                        States = [JobState.Enqueued, JobState.Processing],
                        PageNumber = 1,
                        PageSize = int.MaxValue
                    };

                    var batchResult = await metadataRepository.QueryInstancesAsync(batchQuery, cancellationToken);
                    var instancesByJob = batchResult.Items.GroupBy(i => i.JobKey);

                    foreach (var group in instancesByJob)
                    {
                        if (!_statistics.TryGetValue(group.Key, out var statistic))
                            continue;

                        foreach (var instance in group)
                        {
                            if (instance.State == JobState.Enqueued)
                            {
                                statistic.ReserveSlot(instance.InstanceId);
                            }
                            else if (instance.State == JobState.Processing &&
                                     !string.IsNullOrEmpty(instance.RunningClientId) &&
                                     instance.StartedAt.HasValue)
                            {
                                statistic.AddInstance(new RunningJobInfo
                                {
                                    InstanceId = instance.InstanceId,
                                    WorkerClientId = instance.RunningClientId,
                                    StartedAt = instance.StartedAt.Value
                                });
                            }
                        }
                    }
                }
            }
            finally
            {
                // 5. Release all locks
                foreach (var semaphore in allLocks)
                {
                    semaphore.Release();
                }
            }

            // 6. Check state after reconciliation
            var stateAfter = await CheckConsistencyAsync(cancellationToken);

            stopwatch.Stop();

            RecordState(
                $"Reconciliation completed in {stopwatch.ElapsedMilliseconds}ms. Deviation before: {stateBefore.TotalDeviation}, after: {stateAfter.TotalDeviation}",
                logLevel: LogLevel.Information);

            return ReconcileResult.Ok(stateBefore, stateAfter, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordState($"Reconciliation failed after {stopwatch.ElapsedMilliseconds}ms", logLevel: LogLevel.Error, exception: ex);
            return ReconcileResult.Fail($"Reconciliation failed: {ex.Message}");
        }
    }

    private async Task OnJobStartedAsync(JobStartedEvent evt)
    {
        if (!string.Equals(evt.SchedulerScopeKey, _jobSchedulerOptions.SchedulerScopeKey, StringComparison.Ordinal))
        {
            RecordState($"Ignored JobStartedEvent for foreign scope {evt.SchedulerScopeKey}", logLevel: LogLevel.Debug);
            return;
        }

        if (!_statistics.TryGetValue(evt.JobKey, out var statistic))
        {
            RecordState(
                $"Received JobStartedEvent for unknown job {evt.JobKey}, instance {evt.InstanceId}",
                logLevel: LogLevel.Warning);
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

            RecordState(
                $"Job {evt.JobKey} instance {evt.InstanceId} started on worker {evt.WorkerClientId}, current executing: {statistic.CurrentExecutingCount}/{statistic.MaxConcurrency}",
                logLevel: LogLevel.Debug);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task OnJobCompletedAsync(JobCompletedEvent evt)
    {
        if (!string.Equals(evt.SchedulerScopeKey, _jobSchedulerOptions.SchedulerScopeKey, StringComparison.Ordinal))
        {
            RecordState($"Ignored JobCompletedEvent for foreign scope {evt.SchedulerScopeKey}", logLevel: LogLevel.Debug);
            return;
        }

        if (!_statistics.TryGetValue(evt.JobKey, out var statistic))
        {
            RecordState(
                $"Received JobCompletedEvent for unknown job {evt.JobKey}, instance {evt.InstanceId}",
                logLevel: LogLevel.Warning);
            return;
        }

        var semaphore = _jobLocks.GetOrAdd(evt.JobKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            var (removedFromPending, removedFromRunning) = statistic.RemoveInstanceFromTracking(evt.InstanceId);

            if (removedFromPending || removedFromRunning)
            {
                RecordState(
                    $"Job {evt.JobKey} instance {evt.InstanceId} completed with state {evt.FinalState} and removed from tracking (pending: {removedFromPending}, running: {removedFromRunning}), current executing: {statistic.CurrentExecutingCount}/{statistic.MaxConcurrency}",
                    logLevel: LogLevel.Debug);
            }
            else
            {
                RecordState(
                    $"Job {evt.JobKey} instance {evt.InstanceId} completed but was not found in tracking",
                    logLevel: LogLevel.Warning);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Handles JobDefinitionsChangedEvent to add or update concurrency tracking for job definition changes after startup.
    /// </summary>
    private Task OnJobDefinitionsChangedAsync(JobDefinitionsChangedEvent evt)
    {
        if (!string.Equals(evt.SchedulerScopeKey, _jobSchedulerOptions.SchedulerScopeKey, StringComparison.Ordinal))
        {
            RecordState($"Ignored JobDefinitionsChangedEvent for foreign scope {evt.SchedulerScopeKey}", logLevel: LogLevel.Debug);
            return Task.CompletedTask;
        }

        RecordState(
            $"Received JobDefinitionsChangedEvent: {evt.AddedJobKeys.Count} added, {evt.UpdatedJobKeys.Count} updated, {evt.DeletedJobKeys.Count} deleted",
            logLevel: LogLevel.Debug);

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

                RecordState(
                    $"Added concurrency tracking for new job: {definition.JobKey} (MaxConcurrency: {definition.MaxConcurrency})",
                    logLevel: LogLevel.Debug);
            }
        }

        // 2. Handle updated jobs (update MaxConcurrency if changed)
        foreach (var definition in evt.UpdatedDefinitions)
        {
            if (_statistics.TryGetValue(definition.JobKey, out var statistic))
            {
                statistic.MaxConcurrency = definition.MaxConcurrency;
                RecordState(
                    $"Updated MaxConcurrency for job {definition.JobKey} to {definition.MaxConcurrency}",
                    logLevel: LogLevel.Debug);
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
