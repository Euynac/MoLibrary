using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Modules;

namespace Monica.JobScheduler.ControlPlane;

/// <summary>
/// Executes history cleanup logic that can be shared between scheduled cleanup and manual triggers.
/// </summary>
public class JobHistoryCleanupExecutor(
    IMoJobMetadataRepository metadataRepository,
    ILogger<JobHistoryCleanupExecutor> logger,
    IOptions<ModuleJobSchedulerOption> options)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = options.Value;

    /// <summary>
    /// Performs history cleanup using optimized single-pass batch processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed cleanup result.</returns>
    public async Task<HistoryCleanupResult> ExecuteCleanupAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var cleanupTime = DateTime.UtcNow;

        logger.LogInformation("History cleanup execution started");

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
            logger.LogDebug("Cleanup completed: No instances to clean up");

            return new HistoryCleanupResult
            {
                DeletedCount = 0,
                DeletedInstanceIds = [],
                DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                CleanupTime = cleanupTime
            };
        }

        // Step 3: Batch delete
        var deletedCount = await metadataRepository.DeleteInstancesAsync(candidateIds, cancellationToken);

        stopwatch.Stop();

        logger.LogInformation(
            "Cleanup completed: Deleted {DeletedCount} instances in {DurationSeconds:F2}s",
            deletedCount,
            stopwatch.Elapsed.TotalSeconds);

        return new HistoryCleanupResult
        {
            DeletedCount = deletedCount,
            DeletedInstanceIds = candidateIds,
            DurationSeconds = stopwatch.Elapsed.TotalSeconds,
            CleanupTime = cleanupTime
        };
    }
}
