using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.RegisterCentre.Core;
using MoLibrary.RegisterCentre.Events;
using MoLibrary.RegisterCentre.Interfaces;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Background service that cleans up old job execution history based on retention policies.
/// Extends CoordinatedLeaderService for leader-only execution to prevent duplicate cleanup operations.
/// Supports dynamic leader status changes - stops cleanup loop on leader loss and resumes on leader gain.
/// </summary>
public class JobHistoryCleanupService(
    IMoJobMetadataRepository metadataRepository,
    ILogger<JobHistoryCleanupService> logger,
    ILeaderElectionService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleRegisterCentreOption> registerCentreOptions
) : CoordinatedLeaderService(leaderService, registerCentreOptions, logger, coordinator, observableManager, hostedServiceOptions)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = options.Value;

    public override string ServiceName => nameof(JobHistoryCleanupService);

    protected override Task LeaderInitializeAsync(CancellationToken cancellationToken)
    {
        RecordState(
            $"History cleanup configured: Interval={_jobSchedulerOptions.HistoryCleanupInterval}, MaxDeletionsPerJob={_jobSchedulerOptions.MaxDeletionsPerJobPerCycle}",
            givenLogLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs when leader status is lost. The background cleanup loop will automatically stop
    /// because the leader token is cancelled.
    /// </summary>
    protected override Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        RecordState($"History cleanup service stopped after losing leader status (reason: {reason})", givenLogLevel: LogLevel.Information);
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
                RecordState("Error during history cleanup scan", givenLogLevel: LogLevel.Error, exception: ex);
            }
        }
    }

    /// <summary>
    /// Performs history cleanup using optimized single-pass batch processing.
    /// </summary>
    private async Task CleanupHistoryAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        RecordState("History cleanup scan started", givenLogLevel: LogLevel.Information);

        try
        {
            // Step 1: Query all job definitions to build retention policy map
            var definitionsQuery = new JobDefinitionQuery
            {
                IncludeDeleted = false, // Only active definitions for policies
                PageSize = 10000
            };

            var definitionsResult = await metadataRepository.QueryDefinitionsAsync(definitionsQuery, cancellationToken);

            // Build retention policies dictionary
            var retentionPolicies = definitionsResult.Items.ToDictionary(
                d => d.JobKey,
                d => (d.MaxRetainedHistoryRecords, d.MaxRetentionDays));

            // Step 2: Get cleanup candidates in single batch query (optimized with projection)
            var candidateIds = await metadataRepository.GetCleanupCandidatesAsync(
                retentionPolicies,
                maxRetainedOrphanedInstances: _jobSchedulerOptions.MaxRetainedOrphanedInstances,
                maxDeletionsPerCycle: _jobSchedulerOptions.MaxDeletionsPerJobPerCycle,
                cancellationToken);

            if (candidateIds.Count == 0)
            {
                stopwatch.Stop();
                RecordState("Scan completed: No instances to clean up", givenLogLevel: LogLevel.Debug);
                return;
            }

            // Step 3: Batch delete
            var deletedCount = await metadataRepository.DeleteInstancesAsync(candidateIds, cancellationToken);

            stopwatch.Stop();
            RecordState(
                $"Scan completed: Deleted {deletedCount} instances in {stopwatch.Elapsed.TotalSeconds:F2}s",
                givenLogLevel: LogLevel.Information);
        }
        catch (Exception ex)
        {
            RecordState("Scan failed", givenLogLevel: LogLevel.Error, exception: ex);
        }
    }
}
