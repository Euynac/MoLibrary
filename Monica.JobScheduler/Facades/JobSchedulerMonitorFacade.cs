using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Facades;

/// <summary>
/// Monitor-oriented entry points for realtime scheduler diagnostics.
/// </summary>
public class JobSchedulerMonitorFacade(
    IJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    IJobConcurrencyGuard concurrencyGuard,
    ILogger<JobSchedulerMonitorFacade> logger)
{
    /// <summary>
    /// Gets the combined monitor overview state.
    /// </summary>
    public async Task<Res<MonitorState>> GetMonitorStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var activeQuery = new JobInstanceQuery
            {
                States = [JobState.Enqueued, JobState.Scheduled, JobState.Processing],
                PageNumber = 1,
                PageSize = 500,
                SortBy = "CreatedAt",
                SortDescending = true
            };

            var instancesTask = metadataRepository.QueryInstancesAsync(activeQuery, cancellationToken);
            var definitionsTask = cacheService.GetAllDefinitionsAsync(cancellationToken);
            var executionStatsTask = concurrencyGuard.GetAllExecutionStatisticsAsync(cancellationToken);
            var enqueuedTask = CountInstancesAsync(JobState.Enqueued, cancellationToken);
            var scheduledTask = CountInstancesAsync(JobState.Scheduled, cancellationToken);
            var processingTask = CountInstancesAsync(JobState.Processing, cancellationToken);

            await Task.WhenAll(
                instancesTask,
                definitionsTask,
                executionStatsTask,
                enqueuedTask,
                scheduledTask,
                processingTask);

            var definitions = (await definitionsTask).ToDictionary(d => d.JobKey, d => d);
            var liveExecutions = (await instancesTask).Items
                .Select(instance =>
                {
                    var definition = definitions.GetValueOrDefault(instance.JobKey);
                    return new LiveExecution
                    {
                        InstanceId = instance.InstanceId,
                        JobKey = instance.JobKey,
                        JobName = definition?.JobName ?? instance.JobKey,
                        State = instance.State,
                        CreatedAt = instance.CreatedAt,
                        StartedAt = instance.StartedAt,
                        ElapsedTime = instance.StartedAt.HasValue ? now - instance.StartedAt.Value : null,
                        WorkerClientId = instance.RunningClientId,
                        MaxExecutionTimeout = definition?.MaxExecutionTimeout ?? TimeSpan.FromHours(1)
                    };
                })
                .ToList();

            var jobNames = definitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.JobName);
            var usages = (await executionStatsTask)
                .Where(kvp => kvp.Value.CurrentExecutingCount > 0)
                .Select(kvp => new ConcurrencyUsage
                {
                    JobKey = kvp.Key,
                    JobName = jobNames.GetValueOrDefault(kvp.Key, kvp.Key),
                    CurrentRunning = kvp.Value.RunningCount,
                    PendingCount = kvp.Value.PendingCount,
                    MaxConcurrency = kvp.Value.MaxConcurrency
                })
                .OrderByDescending(usage => usage.UtilizationPercent)
                .ThenBy(usage => usage.JobName)
                .ToList();

            return Res.Ok(new MonitorState
            {
                LiveExecutions = liveExecutions,
                QueueStatus = new QueueStatus
                {
                    EnqueuedCount = await enqueuedTask,
                    ScheduledCount = await scheduledTask,
                    ProcessingCount = await processingTask
                },
                ConcurrencyUsages = usages,
                LastRefreshTime = now
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get monitor state");
            return Res.Fail($"Failed to get monitor state: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets the detailed concurrency state used by the monitor diagnostics panel.
    /// </summary>
    public async Task<Res<ConcurrencyMonitorState>> GetConcurrencyMonitorStateAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var detailedStatsTask = concurrencyGuard.GetDetailedExecutionStatisticsAsync(cancellationToken);
            var definitionsTask = cacheService.GetAllDefinitionsAsync(cancellationToken);
            var consistencyTask = concurrencyGuard.CheckConsistencyAsync(cancellationToken);

            await Task.WhenAll(detailedStatsTask, definitionsTask, consistencyTask);

            var jobNames = (await definitionsTask).ToDictionary(d => d.JobKey, d => d.JobName);
            var details = (await detailedStatsTask)
                .Where(kvp => kvp.Value.CurrentExecutingCount > 0)
                .Select(kvp => new ConcurrencyStatusWithName
                {
                    JobName = jobNames.GetValueOrDefault(kvp.Key, kvp.Key),
                    Statistic = kvp.Value
                })
                .OrderByDescending(detail => detail.UtilizationPercent)
                .ToList();

            return Res.Ok(new ConcurrencyMonitorState
            {
                Details = details,
                Consistency = await consistencyTask,
                LastRefreshTime = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get concurrency monitor state");
            return Res.Fail($"Failed to get concurrency monitor state: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Reconciles in-memory concurrency tracking with persisted job state.
    /// </summary>
    public async Task<Res<ReconcileResult>> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await concurrencyGuard.ReconcileAsync(cancellationToken);
            return result.Success
                ? Res.Ok(result)
                : Res.Fail(result.ErrorMessage ?? "Reconcile failed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconcile concurrency state");
            return Res.Fail($"Failed to reconcile concurrency state: {ex.GetMessageRecursively()}");
        }
    }

    private async Task<int> CountInstancesAsync(JobState state, CancellationToken cancellationToken)
    {
        var query = new JobInstanceQuery
        {
            States = [state],
            PageNumber = 1,
            PageSize = 1
        };

        return (await metadataRepository.QueryInstancesAsync(query, cancellationToken)).TotalCount;
    }
}
