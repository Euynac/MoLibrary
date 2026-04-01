using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Monica.Core.ObservableInstance.Abstractions;
using Monica.Modules;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Services.Support;

namespace Monica.JobScheduler.ControlPlane;

/// <summary>
/// Background service that detects and cleans up zombie job instances.
/// Zombie instances are jobs stuck in Processing or Enqueued states beyond their timeout limits.
/// Extends CoordinatedLeaderService for leader-only execution.
/// Supports dynamic leader status changes - stops scanning on leader loss and resumes on leader gain.
/// </summary>
public class JobZombieDetectorHostedService(
    IMoJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    JobInstanceManager instanceManager,
    IJobConcurrencyGuard concurrencyGuard,
    ILeaderElectionService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions,
    IRegistrationStateManager? registrationStateManager = null
) : CoordinatedLeaderService(leaderService, serviceDiscoveryOptions, coordinator, observableManager, hostedServiceOptions)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = options.Value;

    public override string ServiceName => nameof(JobZombieDetectorHostedService);

    /// <summary>
    /// Result of zombie detection check.
    /// </summary>
    private sealed class ZombieDetectionResult
    {
        public bool IsZombie { get; private init; }
        public string? Reason { get; private init; }

        public static ZombieDetectionResult NotZombie() => new() { IsZombie = false };
        public static ZombieDetectionResult Zombie(string reason) => new() { IsZombie = true, Reason = reason };
    }

    protected override Task OnBecameLeaderAsync(CancellationToken cancellationToken)
    {
        RecordState($"Zombie detector configured: Interval={_jobSchedulerOptions.ZombieDetectionInterval}, ProcessingMultiplier={_jobSchedulerOptions.ProcessingTimeoutMultiplier}, EnqueuedTimeout={_jobSchedulerOptions.EnqueuedStateTimeout}", logLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs when leader status is lost. The background scanning loop will automatically stop
    /// because the leader token is cancelled.
    /// </summary>
    protected override Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        RecordState($"Zombie detector stopped after losing leader status (reason: {reason})", logLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    protected override async Task LeaderExecuteBackgroundAsync(CancellationToken cancellationToken)
    {
        // Start background scanning task
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_jobSchedulerOptions.ZombieDetectionInterval, cancellationToken);
                await DetectAndCleanupZombiesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                RecordState("Error during zombie detection scan", logLevel: LogLevel.Error, exception: ex);
            }
        }
    }

    /// <summary>
    /// Detects and cleans up zombie job instances in Processing and Enqueued states.
    /// </summary>
    private async Task DetectAndCleanupZombiesAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var processingZombieCount = 0;
        var enqueuedZombieCount = 0;

        RecordState("Zombie detection scan started", logLevel: LogLevel.Information);

        try
        {
            // Check consistency before zombie detection
            var consistencyResult = await concurrencyGuard.CheckConsistencyAsync(cancellationToken);

            if (!consistencyResult.IsConsistent)
            {
                RecordState(
                    $"Concurrency state inconsistency detected (deviation: {consistencyResult.TotalDeviation}). Triggering reconciliation before zombie detection.",
                    logLevel: LogLevel.Warning);

                var reconcileResult = await concurrencyGuard.ReconcileAsync(cancellationToken);

                if (reconcileResult.Success)
                {
                    RecordState(
                        $"Reconciliation completed in {reconcileResult.Duration.TotalMilliseconds:F0}ms. Deviation reduced from {reconcileResult.StateBefore?.TotalDeviation} to {reconcileResult.StateAfter?.TotalDeviation}",
                        logLevel: LogLevel.Information);
                }
                else
                {
                    RecordState(
                        $"Reconciliation failed: {reconcileResult.ErrorMessage}",
                        logLevel: LogLevel.Error);
                }
            }

            // Detect Processing state zombies
            processingZombieCount = await DetectProcessingZombiesAsync(cancellationToken);

            // Detect Enqueued state zombies
            enqueuedZombieCount = await DetectEnqueuedZombiesAsync(cancellationToken);

            stopwatch.Stop();

            if (processingZombieCount > 0 || enqueuedZombieCount > 0)
            {
                RecordState($"Scan completed: Found {processingZombieCount} Processing and {enqueuedZombieCount} Enqueued zombies in {stopwatch.Elapsed.TotalSeconds:F2}s", logLevel: LogLevel.Warning);
            }
            else
            {
                RecordState("Scan completed: No zombies found", logLevel: LogLevel.Information);
            }
        }
        catch (Exception ex)
        {
            RecordState("Scan failed", logLevel: LogLevel.Error, exception: ex);
        }
    }

    /// <summary>
    /// Detects zombie instances in Processing state.
    /// </summary>
    private async Task<int> DetectProcessingZombiesAsync(CancellationToken cancellationToken)
    {
        var zombieCount = 0;
        var pageNumber = 1;
        const int pageSize = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var query = new JobInstanceQuery
            {
                State = JobState.Processing,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            if (result.Items.Count == 0)
            {
                break;
            }

            foreach (var instance in result.Items)
            {
                try
                {
                    var detectionResult = await IsProcessingZombieAsync(instance, cancellationToken);
                    if (detectionResult.IsZombie)
                    {
                        await MarkAsZombieAsync(instance, detectionResult.Reason!, cancellationToken);
                        zombieCount++;
                    }
                }
                catch (Exception ex)
                {
                    RecordState($"Error processing instance {instance.InstanceId}", logLevel: LogLevel.Error, exception: ex);
                }
            }

            // Check if there are more pages
            if (result.Items.Count < pageSize)
            {
                break;
            }

            pageNumber++;
        }

        return zombieCount;
    }

    /// <summary>
    /// Detects zombie instances in Enqueued state.
    /// </summary>
    private async Task<int> DetectEnqueuedZombiesAsync(CancellationToken cancellationToken)
    {
        var zombieCount = 0;
        var pageNumber = 1;
        const int pageSize = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var query = new JobInstanceQuery
            {
                State = JobState.Enqueued,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            if (result.Items.Count == 0)
            {
                break;
            }

            foreach (var instance in result.Items)
            {
                try
                {
                    var detectionResult = IsEnqueuedZombie(instance);
                    if (detectionResult.IsZombie)
                    {
                        await MarkAsZombieAsync(instance, detectionResult.Reason!, cancellationToken);
                        zombieCount++;
                    }
                }
                catch (Exception ex)
                {
                    RecordState($"Error processing instance {instance.InstanceId}", logLevel: LogLevel.Error, exception: ex);
                }
            }

            // Check if there are more pages
            if (result.Items.Count < pageSize)
            {
                break;
            }

            pageNumber++;
        }

        return zombieCount;
    }

    /// <summary>
    /// Checks if a Processing instance is a zombie.
    /// </summary>
    private async Task<ZombieDetectionResult> IsProcessingZombieAsync(JobInstance instance, CancellationToken cancellationToken)
    {
        // Get job definition to determine timeout
        var definition = await cacheService.GetDefinitionAsync(instance.JobKey, cancellationToken);
        if (definition == null)
        {
            var reason = $"Job definition not found for job {instance.JobKey}";
            RecordState($"Zombie detected: Instance {instance.InstanceId} - {reason}", logLevel: LogLevel.Warning);
            return ZombieDetectionResult.Zombie(reason);
        }

        if (definition.IsDisabled)
        {
            var reason = $"Job {instance.JobKey} is disabled";
            RecordState($"Zombie detected: Instance {instance.InstanceId} - {reason}", logLevel: LogLevel.Warning);
            return ZombieDetectionResult.Zombie(reason);
        }

        // Check if worker is still online (if enabled)
        if (_jobSchedulerOptions.CheckWorkerHealthBeforeZombieDetection &&
            registrationStateManager != null &&
            !string.IsNullOrEmpty(instance.RunningClientId))
        {
            var isWorkerOnline = await IsWorkerOnlineAsync(instance.RunningClientId, cancellationToken);
            if (!isWorkerOnline)
            {
                var reason = $"Worker {instance.RunningClientId} is offline";
                RecordState($"Zombie detected: Instance {instance.InstanceId} - {reason}", logLevel: LogLevel.Warning);
                return ZombieDetectionResult.Zombie(reason);
            }
        }

        // Calculate timeout
        if (!instance.StartedAt.HasValue)
        {
            var reason = "StartedAt is null for Processing instance";
            RecordState($"Zombie detected: Instance {instance.InstanceId} - {reason}", logLevel: LogLevel.Warning);
            return ZombieDetectionResult.Zombie(reason);
        }

        var effectiveTimeout = TimeSpan.FromTicks(
            (long)(definition.MaxExecutionTimeout.Ticks * _jobSchedulerOptions.ProcessingTimeoutMultiplier));
        var elapsed = DateTime.UtcNow - instance.StartedAt.Value;

        if (elapsed > effectiveTimeout)
        {
            var reason = $"Execution timeout after {elapsed:hh\\:mm\\:ss} (limit: {effectiveTimeout:hh\\:mm\\:ss})";
            RecordState($"Zombie detected: Instance {instance.InstanceId} - {reason}", logLevel: LogLevel.Warning);
            return ZombieDetectionResult.Zombie(reason);
        }

        return ZombieDetectionResult.NotZombie();
    }

    /// <summary>
    /// Checks if an Enqueued instance is a zombie.
    /// </summary>
    private ZombieDetectionResult IsEnqueuedZombie(JobInstance instance)
    {
        var elapsed = DateTime.UtcNow - instance.CreatedAt;

        if (elapsed > _jobSchedulerOptions.EnqueuedStateTimeout)
        {
            var reason = $"Job stuck in Enqueued state for {elapsed:hh\\:mm\\:ss} (limit: {_jobSchedulerOptions.EnqueuedStateTimeout:hh\\:mm\\:ss})";
            RecordState($"Zombie detected: Instance {instance.InstanceId} - {reason}", logLevel: LogLevel.Warning);
            return ZombieDetectionResult.Zombie(reason);
        }

        return ZombieDetectionResult.NotZombie();
    }

    /// <summary>
    /// Marks a zombie instance as Failed with the specified reason.
    /// </summary>
    private async Task MarkAsZombieAsync(JobInstance instance, string reason, CancellationToken cancellationToken)
    {
        var message = $"{reason}. Marked as zombie by detector.";

        try
        {
            await instanceManager.UpdateStateAsync(
                instance.InstanceId,
                JobState.Failed,
                message,
                cancellationToken);

            RecordState($"Marked zombie instance {instance.InstanceId} as Failed", logLevel: LogLevel.Warning);
        }
        catch (InvalidOperationException ex)
        {
            // State transition may fail if instance already moved to terminal state (race condition)
            RecordState($"Failed to mark instance {instance.InstanceId} as zombie (likely already in terminal state)", logLevel: LogLevel.Warning, exception: ex);
        }
    }

    /// <summary>
    /// Checks whether a worker is online through the service discovery registration state manager.
    /// </summary>
    private async Task<bool> IsWorkerOnlineAsync(string workerId, CancellationToken cancellationToken)
    {
        try
        {
            if (registrationStateManager == null)
            {
                return true; // Assume online if service discovery is unavailable.
            }

            var instances = await registrationStateManager.GetAllInstancesAsync(cancellationToken);

            // Check if worker instance exists in registered instances
            var workerExists = instances.Any(i => i.InstanceId == workerId);

            if (!workerExists)
            {
                // Worker not found in the service registry, so it is offline.
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            RecordState($"Failed to check worker health for {workerId}", logLevel: LogLevel.Warning, exception: ex);
            return true; // Assume online on error
        }
    }
}
