using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.JobScheduler.UI.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 仪表盘数据服务
/// </summary>
public class JobDashboardService(
    IMoJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    IJobConcurrencyGuard concurrencyGuard,
    HealthCheckService healthCheckService,
    IOptions<ModuleJobSchedulerUIOption> uiOptions,
    ILogger<JobDashboardService> logger)
{
    private readonly ModuleJobSchedulerUIOption _options = uiOptions.Value;

    /// <summary>
    /// 获取仪表盘总览数据
    /// </summary>
    public async Task<Res<DashboardSummary>> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var metricsStartTime = now - _options.HealthMetricsWindow;

            // 1. Get all job definitions
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);

            // 2. Count by type
            var recurringCount = definitions.Count(d => d.JobType == JobType.Recurring && !d.IsDeleted);
            var triggeredCount = definitions.Count(d => d.JobType == JobType.Triggered && !d.IsDeleted);
            var disabledCount = definitions.Count(d => d.IsDisabled && !d.IsDeleted);
            var totalJobs = definitions.Count(d => !d.IsDeleted);

            // 3. Get instances for state distribution and metrics
            var instanceQuery = new JobInstanceQuery
            {
                CreatedAfter = metricsStartTime,
                CreatedBefore = now,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var instanceResult = await metadataRepository.QueryInstancesAsync(instanceQuery, cancellationToken);
            var instances = instanceResult.Items;

            // 4. Calculate state distribution
            var stateDistribution = instances
                .GroupBy(i => i.State)
                .ToDictionary(g => g.Key, g => g.Count());

            // 5. Get currently running count from concurrency guard
            var executionStats = await concurrencyGuard.GetAllExecutionStatisticsAsync(cancellationToken);
            var runningNow = executionStats.Values.Sum(s => s.RunningCount);

            // 6. Calculate success rate
            var terminalStates = new[]
            {
                JobState.Succeeded, JobState.Failed, JobState.Terminated,
                JobState.Cancelled, JobState.Skipped
            };
            var completedInstances = instances.Where(i => terminalStates.Contains(i.State)).ToList();
            var succeededCount = completedInstances.Count(i => i.State == JobState.Succeeded);
            var successRate = completedInstances.Count > 0
                ? ((double)succeededCount / completedInstances.Count) * 100
                : 100;

            // 7. Calculate throughput (per hour)
            var hoursInWindow = _options.HealthMetricsWindow.TotalHours;
            var throughputPerHour = hoursInWindow > 0
                ? (int)Math.Round(completedInstances.Count / hoursInWindow)
                : 0;

            // 8. Get system health status
            var (healthStatus, healthMessage) = await GetSystemHealthStatusAsync(cancellationToken);

            return Res.Ok(new DashboardSummary
            {
                TotalJobs = totalJobs,
                RecurringJobCount = recurringCount,
                TriggeredJobCount = triggeredCount,
                DisabledJobCount = disabledCount,
                RunningNow = runningNow,
                SuccessRate = Math.Round(successRate, 1),
                ThroughputPerHour = throughputPerHour,
                StateDistribution = stateDistribution,
                HealthStatus = healthStatus,
                HealthMessage = healthMessage,
                MetricsStartTime = metricsStartTime,
                MetricsEndTime = now
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get dashboard summary");
            return Res.Fail($"获取仪表盘数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取最近活动
    /// </summary>
    public async Task<Res<List<RecentActivity>>> GetRecentActivitiesAsync(
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get recent instances ordered by activity time
            var query = new JobInstanceQuery
            {
                PageNumber = 1,
                PageSize = count,
                SortBy = "CompletedAt",
                SortDescending = true
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

            // Get job definitions for names
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);

            var activities = result.Items.Select(instance => new RecentActivity
            {
                InstanceId = instance.InstanceId,
                JobKey = instance.JobKey,
                JobName = jobNameMap.GetValueOrDefault(instance.JobKey, instance.JobKey),
                State = instance.State,
                Timestamp = instance.CompletedAt ?? instance.StartedAt ?? instance.CreatedAt,
                Duration = instance.StartedAt.HasValue && instance.CompletedAt.HasValue
                    ? instance.CompletedAt.Value - instance.StartedAt.Value
                    : null
            }).ToList();

            return Res.Ok(activities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get recent activities");
            return Res.Fail($"获取最近活动失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取问题任务
    /// </summary>
    public async Task<Res<ProblemJobs>> GetProblemJobsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var metricsStartTime = now - _options.HealthMetricsWindow;

            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);
            var jobConfigMap = definitions.ToDictionary(d => d.JobKey, d => d);

            var problemJobs = new ProblemJobs();

            // 1. Find consecutive failures (jobs with last N executions all failed)
            var consecutiveFailures = await FindConsecutiveFailuresAsync(
                definitions.ToList(), jobNameMap, cancellationToken);
            problemJobs.ConsecutiveFailures = consecutiveFailures;

            // 2. Find long running jobs
            var longRunning = await FindLongRunningJobsAsync(
                jobNameMap, jobConfigMap, cancellationToken);
            problemJobs.LongRunning = longRunning;

            // 3. Find high skip rate jobs
            var highSkipRate = await FindHighSkipRateJobsAsync(
                metricsStartTime, now, jobNameMap, jobConfigMap, cancellationToken);
            problemJobs.HighSkipRate = highSkipRate;

            return Res.Ok(problemJobs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get problem jobs");
            return Res.Fail($"获取问题任务失败: {ex.Message}");
        }
    }

    private async Task<(SystemHealthStatus status, string? message)> GetSystemHealthStatusAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var healthReport = await healthCheckService.CheckHealthAsync(
                registration => registration.Name == "JobScheduler",
                cancellationToken);

            var jobSchedulerEntry = healthReport.Entries.Values.FirstOrDefault();

            if (jobSchedulerEntry.Status == HealthStatus.Healthy)
            {
                return (SystemHealthStatus.Healthy, jobSchedulerEntry.Description);
            }

            if (jobSchedulerEntry.Status == HealthStatus.Degraded)
            {
                return (SystemHealthStatus.Degraded, jobSchedulerEntry.Description);
            }

            return (SystemHealthStatus.Unhealthy, jobSchedulerEntry.Description);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get health check status, assuming healthy");
            return (SystemHealthStatus.Healthy, null);
        }
    }

    private async Task<List<ConsecutiveFailureJob>> FindConsecutiveFailuresAsync(
        List<JobDefinition> definitions,
        Dictionary<string, string> jobNameMap,
        CancellationToken cancellationToken)
    {
        var result = new List<ConsecutiveFailureJob>();
        const int consecutiveThreshold = 3;

        foreach (var definition in definitions.Where(d => !d.IsDeleted && !d.IsDisabled))
        {
            var query = new JobInstanceQuery
            {
                JobKey = definition.JobKey,
                PageNumber = 1,
                PageSize = consecutiveThreshold,
                SortBy = "CreatedAt",
                SortDescending = true
            };

            var instances = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

            if (instances.Items.Count < consecutiveThreshold)
                continue;

            var recentInstances = instances.Items.Take(consecutiveThreshold).ToList();
            var allFailed = recentInstances.All(i =>
                i.State == JobState.Failed || i.State == JobState.Terminated);

            if (allFailed)
            {
                var lastFailure = recentInstances.First();
                result.Add(new ConsecutiveFailureJob
                {
                    JobKey = definition.JobKey,
                    JobName = jobNameMap.GetValueOrDefault(definition.JobKey, definition.JobKey),
                    ConsecutiveFailureCount = consecutiveThreshold,
                    LastFailureTime = lastFailure.CompletedAt ?? lastFailure.CreatedAt,
                    LastFailureInstanceId = lastFailure.InstanceId
                });
            }
        }

        return result.OrderByDescending(j => j.LastFailureTime).Take(10).ToList();
    }

    private async Task<List<LongRunningJob>> FindLongRunningJobsAsync(
        Dictionary<string, string> jobNameMap,
        Dictionary<string, JobDefinition> jobConfigMap,
        CancellationToken cancellationToken)
    {
        var result = new List<LongRunningJob>();
        var now = DateTime.UtcNow;

        // Get all processing instances
        var query = new JobInstanceQuery
        {
            States = [JobState.Processing],
            PageNumber = 1,
            PageSize = 100
        };

        var instances = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

        foreach (var instance in instances.Items.Where(i => i.StartedAt.HasValue))
        {
            var elapsed = now - instance.StartedAt!.Value;
            var maxTimeout = jobConfigMap.TryGetValue(instance.JobKey, out var def)
                ? def.MaxExecutionTimeout
                : TimeSpan.FromHours(1);

            // Report if elapsed > 50% of max timeout
            if (elapsed.TotalSeconds > maxTimeout.TotalSeconds * 0.5)
            {
                result.Add(new LongRunningJob
                {
                    InstanceId = instance.InstanceId,
                    JobKey = instance.JobKey,
                    JobName = jobNameMap.GetValueOrDefault(instance.JobKey, instance.JobKey),
                    ElapsedTime = elapsed,
                    MaxExecutionTimeout = maxTimeout,
                    StartedAt = instance.StartedAt.Value
                });
            }
        }

        return result.OrderByDescending(j => j.TimeoutPercentage).Take(10).ToList();
    }

    private async Task<List<HighSkipRateJob>> FindHighSkipRateJobsAsync(
        DateTime startTime,
        DateTime endTime,
        Dictionary<string, string> jobNameMap,
        Dictionary<string, JobDefinition> jobConfigMap,
        CancellationToken cancellationToken)
    {
        var result = new List<HighSkipRateJob>();
        const double skipRateThreshold = 10.0; // 10%

        var query = new JobInstanceQuery
        {
            CreatedAfter = startTime,
            CreatedBefore = endTime,
            PageNumber = 1,
            PageSize = int.MaxValue
        };

        var instances = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

        var groupedByJob = instances.Items
            .GroupBy(i => i.JobKey)
            .Select(g => new
            {
                JobKey = g.Key,
                TotalCount = g.Count(),
                SkippedCount = g.Count(i => i.State == JobState.Skipped)
            })
            .Where(g => g.TotalCount >= 10 && g.SkippedCount > 0); // At least 10 executions

        foreach (var group in groupedByJob)
        {
            var skipRate = ((double)group.SkippedCount / group.TotalCount) * 100;
            if (skipRate >= skipRateThreshold)
            {
                result.Add(new HighSkipRateJob
                {
                    JobKey = group.JobKey,
                    JobName = jobNameMap.GetValueOrDefault(group.JobKey, group.JobKey),
                    SkippedCount = group.SkippedCount,
                    TotalCount = group.TotalCount,
                    MaxConcurrency = jobConfigMap.TryGetValue(group.JobKey, out var def)
                        ? def.MaxConcurrency
                        : 1
                });
            }
        }

        return result.OrderByDescending(j => j.SkipRate).Take(10).ToList();
    }
}
