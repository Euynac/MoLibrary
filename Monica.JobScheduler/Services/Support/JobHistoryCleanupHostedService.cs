using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Services.Support;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Background service that cleans up old job execution history based on retention policies.
/// Extends CoordinatedLeaderService for leader-only execution to prevent duplicate cleanup operations.
/// Supports dynamic leader status changes - stops cleanup loop on leader loss and resumes on leader gain.
/// </summary>
public class JobHistoryCleanupHostedService(
    JobHistoryCleanupExecutor executor,
    ILeaderElectionService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions
) : CoordinatedLeaderService(leaderService, serviceDiscoveryOptions, coordinator, observableManager, hostedServiceOptions)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = options.Value;

    public override string ServiceName => nameof(JobHistoryCleanupHostedService);

    protected override Task OnBecameLeaderAsync(CancellationToken cancellationToken)
    {
        RecordState(
            $"History cleanup configured: Interval={_jobSchedulerOptions.HistoryCleanupInterval}, MaxDeletionsPerJob={_jobSchedulerOptions.MaxDeletionsPerJobPerCycle}",
            logLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs when leader status is lost. The background cleanup loop will automatically stop
    /// because the leader token is cancelled.
    /// </summary>
    protected override Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        RecordState($"History cleanup service stopped after losing leader status (reason: {reason})", logLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    protected override async Task LeaderExecuteBackgroundAsync(CancellationToken cancellationToken)
    {
        // Start background scanning task
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
#if DEBUG
                await Task.Delay(10000, cancellationToken);
                await CleanupHistoryAsync(cancellationToken);
                await Task.Delay(_jobSchedulerOptions.HistoryCleanupInterval, cancellationToken);
#else
                await Task.Delay(_jobSchedulerOptions.HistoryCleanupInterval, cancellationToken);
                await CleanupHistoryAsync(cancellationToken);
#endif


            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                RecordState("Error during history cleanup scan", logLevel: LogLevel.Error, exception: ex);
            }
        }
    }

    /// <summary>
    /// Performs history cleanup using optimized single-pass batch processing.
    /// </summary>
    private async Task CleanupHistoryAsync(CancellationToken cancellationToken)
    {
        RecordState("History cleanup scan started", logLevel: LogLevel.Information);

        try
        {
            var result = await executor.ExecuteCleanupAsync(cancellationToken);

            RecordState(
                $"Scan completed: Deleted {result.DeletedCount} instances in {result.DurationSeconds:F2}s",
                logLevel: LogLevel.Information);
        }
        catch (Exception ex)
        {
            RecordState("Scan failed", logLevel: LogLevel.Error, exception: ex);
        }
    }
}
