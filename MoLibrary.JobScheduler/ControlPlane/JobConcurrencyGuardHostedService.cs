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
/// </summary>
public class JobConcurrencyGuardHostedService(
    IJobDefinitionCacheService cacheService,
    IMoJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    JobInstanceManager instanceManager,
    ILogger<JobConcurrencyGuardHostedService> logger,
    ILeaderService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IRegisterCentreServer? registerCentreServer = null) : CoordinatedLeaderService(leaderService, options, logger, coordinator, observableManager, hostedServiceOptions), IJobConcurrencyGuard
{
    private readonly ConcurrentDictionary<string, JobExecutionStatistic> _statistics = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _jobLocks = new();
    private readonly List<IAsyncDisposable> _eventSubscriptions = [];

    public override string ServiceName => nameof(JobConcurrencyGuardHostedService);

    protected override async Task LeaderInitializeAsync(CancellationToken cancellationToken)
    {
        await InitializeConcurrencyTrackingAsync(cancellationToken);
    }

    private async Task InitializeConcurrencyTrackingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobConcurrencyGuard is initializing...");

        // 1. Load all job definitions from cache
        var definitions = await cacheService.GetAllJobDefinitionsAsync(cancellationToken);
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

        // 3. Scan all Processing state instances to recover in-memory state
        foreach (var definition in definitions)
        {
            var query = new JobInstanceQuery
            {
                JobKey = definition.JobKey,
                State = JobState.Processing,
                PageNumber = 1,
                PageSize = int.MaxValue
            };
            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            var processingInstances = result.Items;

            foreach (var instance in processingInstances)
            {
                if (string.IsNullOrEmpty(instance.RunningClientId) || !instance.StartedAt.HasValue)
                {
                    logger.LogWarning(
                        "Found Processing instance {InstanceId} with missing client ID or started time, skipping recovery",
                        instance.InstanceId);
                    continue;
                }

                _statistics[definition.JobKey].AddInstance(new RunningJobInfo
                {
                    InstanceId = instance.InstanceId,
                    WorkerClientId = instance.RunningClientId,
                    StartedAt = instance.StartedAt.Value
                });

                logger.LogDebug(
                    "Recovered running instance {InstanceId} for job {JobKey} on worker {WorkerId}",
                    instance.InstanceId,
                    definition.JobKey,
                    instance.RunningClientId);
            }
        }

        // 4. Subscribe to lifecycle events
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobStartedEvent>(OnJobStartedAsync));
        _eventSubscriptions.Add(await eventBus.SubscribeAsync<JobCompletedEvent>(OnJobCompletedAsync));

        // 5. Subscribe to RegisterCentre offline event (if available)
        if (registerCentreServer != null)
        {
            registerCentreServer.ServiceInstanceOffline += OnServiceInstanceOfflineHandler;
            logger.LogDebug("Subscribed to RegisterCentre ServiceInstanceOffline event");
        }
        else
        {
            logger.LogWarning("RegisterCentre server is not available, worker offline detection will not work");
        }

        logger.LogInformation(
            "JobConcurrencyGuard initialized with {DefinitionCount} job definitions and {RunningCount} recovered running instances",
            _statistics.Count,
            _statistics.Values.Sum(s => s.CurrentExecutingCount));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobConcurrencyGuard is stopping...");

        try
        {
            // Call base to stop ExecuteAsync
            await base.StopAsync(cancellationToken);

            // Unsubscribe from EventBus events
            foreach (var subscription in _eventSubscriptions)
            {
                await subscription.DisposeAsync();
            }
            _eventSubscriptions.Clear();

            // Unsubscribe from RegisterCentre event
            if (registerCentreServer != null)
            {
                registerCentreServer.ServiceInstanceOffline -= OnServiceInstanceOfflineHandler;
            }

            // Dispose all semaphores
            foreach (var semaphore in _jobLocks.Values)
            {
                semaphore.Dispose();
            }
            _jobLocks.Clear();

            logger.LogInformation("JobConcurrencyGuard stopped");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during JobConcurrencyGuard shutdown");
            throw;
        }
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
                    "Job {JobKey} exceeded MaxConcurrency limit: {Current}/{Max}",
                    jobKey,
                    statistic.CurrentExecutingCount,
                    statistic.MaxConcurrency);
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

    public async Task<bool> TryReserveExecutionSlotAsync(
        string jobKey,
        string instanceId,
        CancellationToken cancellationToken = default)
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
            if (!statistic.CanExecute)
            {
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} exceeded MaxConcurrency: {Current}/{Max}",
                    jobKey,
                    instanceId,
                    statistic.CurrentExecutingCount,
                    statistic.MaxConcurrency);
                return false;
            }

            // Reserve the slot
            statistic.ReserveSlot(instanceId);

            logger.LogDebug(
                "Reserved execution slot for job {JobKey} instance {InstanceId}: {Current}/{Max}",
                jobKey,
                instanceId,
                statistic.CurrentExecutingCount,
                statistic.MaxConcurrency);

            return true;
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
    /// Handler for RegisterCentre's C# event
    /// </summary>
    private void OnServiceInstanceOfflineHandler(object? sender, ServiceInstanceOfflineEvent evt)
    {
        // Call the async method without awaiting (fire-and-forget)
        // Since this is an event handler, we can't await
        _ = OnWorkerOfflineAsync(evt);
    }

    private async Task OnWorkerOfflineAsync(ServiceInstanceOfflineEvent evt)
    {
        logger.LogWarning(
            "Worker instance {InstanceId} from project {ProjectName} went offline, cleaning up orphaned jobs",
            evt.InstanceId,
            evt.ProjectName);

        var orphanedCount = 0;

        foreach (var (jobKey, statistic) in _statistics)
        {
            var semaphore = _jobLocks.GetOrAdd(jobKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                var orphanedInstances = statistic.RemoveInstancesByWorker(evt.InstanceId);

                foreach (var orphaned in orphanedInstances)
                {
                    // Mark the orphaned instance as Failed
                    await instanceManager.UpdateStateAsync(
                        orphaned.InstanceId,
                        JobState.Failed,
                        message: $"Worker instance {evt.InstanceId} went offline at {evt.OfflineTime:yyyy-MM-dd HH:mm:ss} UTC");

                    logger.LogWarning(
                        "Marked orphaned job instance {InstanceId} (job {JobKey}) as Failed due to worker {WorkerId} offline",
                        orphaned.InstanceId,
                        jobKey,
                        evt.InstanceId);

                    orphanedCount++;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        logger.LogInformation(
            "Cleaned up {OrphanedCount} orphaned job instances from offline worker {InstanceId}",
            orphanedCount,
            evt.InstanceId);
    }
}
