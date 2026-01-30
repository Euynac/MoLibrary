using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.Models;
using Monica.Tool.MoResponse;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// 实时监控服务
/// </summary>
public class JobMonitorService(
    IMoJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    IJobConcurrencyGuard concurrencyGuard,
    ILogger<JobMonitorService> logger)
{
    /// <summary>
    /// 获取实时执行列表
    /// </summary>
    public async Task<Res<List<LiveExecution>>> GetLiveExecutionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Get all active instances (non-terminal states)
            var query = new JobInstanceQuery
            {
                States = [JobState.Enqueued, JobState.Scheduled, JobState.Processing],
                PageNumber = 1,
                PageSize = 500,
                SortBy = "CreatedAt",
                SortDescending = true
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

            // Get job definitions for names and timeouts
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobMap = definitions.ToDictionary(d => d.JobKey, d => d);

            var liveExecutions = result.Items.Select(instance =>
            {
                var def = jobMap.GetValueOrDefault(instance.JobKey);
                var elapsed = instance.StartedAt.HasValue ? now - instance.StartedAt.Value : (TimeSpan?)null;

                return new LiveExecution
                {
                    InstanceId = instance.InstanceId,
                    JobKey = instance.JobKey,
                    JobName = def?.JobName ?? instance.JobKey,
                    State = instance.State,
                    CreatedAt = instance.CreatedAt,
                    StartedAt = instance.StartedAt,
                    ElapsedTime = elapsed,
                    WorkerClientId = instance.RunningClientId,
                    MaxExecutionTimeout = def?.MaxExecutionTimeout ?? TimeSpan.FromHours(1)
                };
            }).ToList();

            return Res.Ok(liveExecutions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get live executions");
            return Res.Fail($"获取实时执行列表失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取队列状态
    /// </summary>
    public async Task<Res<QueueStatus>> GetQueueStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get counts for each state
            var enqueuedQuery = new JobInstanceQuery
            {
                States = [JobState.Enqueued],
                PageNumber = 1,
                PageSize = 1
            };
            var enqueuedResult = await metadataRepository.QueryInstancesAsync(enqueuedQuery, cancellationToken);

            var scheduledQuery = new JobInstanceQuery
            {
                States = [JobState.Scheduled],
                PageNumber = 1,
                PageSize = 1
            };
            var scheduledResult = await metadataRepository.QueryInstancesAsync(scheduledQuery, cancellationToken);

            var processingQuery = new JobInstanceQuery
            {
                States = [JobState.Processing],
                PageNumber = 1,
                PageSize = 1
            };
            var processingResult = await metadataRepository.QueryInstancesAsync(processingQuery, cancellationToken);

            return Res.Ok(new QueueStatus
            {
                EnqueuedCount = enqueuedResult.TotalCount,
                ScheduledCount = scheduledResult.TotalCount,
                ProcessingCount = processingResult.TotalCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get queue status");
            return Res.Fail($"获取队列状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取并发使用情况
    /// </summary>
    public async Task<Res<List<ConcurrencyUsage>>> GetConcurrencyUsageAsync(
        bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get real-time statistics from concurrency guard
            var statistics = await concurrencyGuard.GetAllExecutionStatisticsAsync(cancellationToken);

            // Get job definitions for names
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);

            var usages = statistics
                .Where(kvp => !onlyActive || kvp.Value.CurrentExecutingCount > 0)
                .Select(kvp => new ConcurrencyUsage
                {
                    JobKey = kvp.Key,
                    JobName = jobNameMap.GetValueOrDefault(kvp.Key, kvp.Key),
                    CurrentRunning = kvp.Value.RunningCount,
                    PendingCount = kvp.Value.PendingCount,
                    MaxConcurrency = kvp.Value.MaxConcurrency
                })
                .OrderByDescending(u => u.UtilizationPercent)
                .ThenBy(u => u.JobName)
                .ToList();

            return Res.Ok(usages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get concurrency usage");
            return Res.Fail($"获取并发使用情况失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取完整的监控状态（一次性获取所有数据）
    /// </summary>
    public async Task<Res<MonitorState>> GetMonitorStateAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var liveExecutionsTask = GetLiveExecutionsAsync(cancellationToken);
            var queueStatusTask = GetQueueStatusAsync(cancellationToken);
            var concurrencyUsageTask = GetConcurrencyUsageAsync(true, cancellationToken);

            await Task.WhenAll(liveExecutionsTask, queueStatusTask, concurrencyUsageTask);

            var liveExecutionsResult = await liveExecutionsTask;
            var queueStatusResult = await queueStatusTask;
            var concurrencyUsageResult = await concurrencyUsageTask;

            if (liveExecutionsResult.IsFailed(out var error1, out _))
                return Res.Fail(error1.Message ?? "获取实时执行失败");

            if (queueStatusResult.IsFailed(out var error2, out _))
                return Res.Fail(error2.Message ?? "获取队列状态失败");

            if (concurrencyUsageResult.IsFailed(out var error3, out _))
                return Res.Fail(error3.Message ?? "获取并发使用情况失败");

            return Res.Ok(new MonitorState
            {
                LiveExecutions = liveExecutionsResult.Data!,
                QueueStatus = queueStatusResult.Data!,
                ConcurrencyUsages = concurrencyUsageResult.Data!,
                LastRefreshTime = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get monitor state");
            return Res.Fail($"获取监控状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取详细的并发监控状态（包含实例列表和一致性检测）
    /// </summary>
    public async Task<Res<ConcurrencyMonitorState>> GetConcurrencyMonitorStateAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get detailed statistics from concurrency guard
            var detailedStats = await concurrencyGuard.GetDetailedExecutionStatisticsAsync(cancellationToken);

            // Get job definitions for names
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);

            // Check consistency
            var consistencyResult = await concurrencyGuard.CheckConsistencyAsync(cancellationToken);

            var details = detailedStats
                .Where(kvp => kvp.Value.CurrentExecutingCount > 0)
                .Select(kvp => new ConcurrencyStatusWithName
                {
                    JobName = jobNameMap.GetValueOrDefault(kvp.Key, kvp.Key),
                    Statistic = kvp.Value
                })
                .OrderByDescending(u => u.UtilizationPercent)
                .ToList();

            return Res.Ok(new ConcurrencyMonitorState
            {
                Details = details,
                Consistency = consistencyResult,
                LastRefreshTime = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get concurrency monitor state");
            return Res.Fail($"获取并发监控状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 触发并发状态同步
    /// </summary>
    public async Task<Res<ReconcileResult>> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await concurrencyGuard.ReconcileAsync(cancellationToken);

            if (result.Success)
            {
                return Res.Ok(result);
            }

            return Res.Fail(result.ErrorMessage ?? "同步失败");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconcile concurrency state");
            return Res.Fail($"同步失败: {ex.Message}");
        }
    }
}
