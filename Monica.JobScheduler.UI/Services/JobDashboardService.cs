using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.Models;
using Monica.Modules;
using Monica.Tool.Results;
using Microsoft.Extensions.Localization;
using Monica.JobScheduler.UI.Localization;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// Dashboard data service - pure in-memory processing based on preloaded data
/// </summary>
public class JobDashboardService(
    IJobConcurrencyGuard concurrencyGuard,
    HealthCheckService healthCheckService,
    IOptions<ModuleJobSchedulerUIOption> uiOptions,
    ILogger<JobDashboardService> logger,
    IStringLocalizer<JobSchedulerResource> localizer)
{
    private readonly ModuleJobSchedulerUIOption _options = uiOptions.Value;

    /// <summary>
    /// Build dashboard overview data (based on preloaded data, pure memory processing)
    /// </summary>
    public async Task<Res<DashboardSummary>> BuildDashboardSummaryAsync(
        DashboardDataContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Count by type (from pre-loaded definitions)
            var definitions = context.Definitions;
            var recurringCount = definitions.Count(d => d.JobType == JobType.Recurring && !d.IsDeleted);
            var triggeredCount = definitions.Count(d => d.JobType == JobType.Triggered && !d.IsDeleted);
            var disabledCount = definitions.Count(d => d.IsDisabled && !d.IsDeleted);
            var totalJobs = definitions.Count(d => !d.IsDeleted);

            // 2. Use pre-loaded state distribution
            var stateDistribution = context.StateDistribution.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 3. Get currently running count from concurrency guard (real-time data)
            var executionStats = await concurrencyGuard.GetAllExecutionStatisticsAsync(cancellationToken);
            var runningNow = executionStats.Values.Sum(s => s.RunningCount);

            // 4. Calculate success rate from state distribution counts
            var terminalStates = new[]
            {
                JobState.Succeeded, JobState.Failed, JobState.Terminated,
                JobState.Cancelled, JobState.Skipped
            };
            var completedCount = terminalStates.Sum(s => stateDistribution.GetValueOrDefault(s, 0));
            var succeededCount = stateDistribution.GetValueOrDefault(JobState.Succeeded, 0);
            var successRate = completedCount > 0
                ? ((double)succeededCount / completedCount) * 100
                : 100;

            // 5. Calculate throughput (per hour)
            var hoursInWindow = _options.HealthMetricsWindow.TotalHours;
            var throughputPerHour = hoursInWindow > 0
                ? (int)Math.Round(completedCount / hoursInWindow)
                : 0;

            // 6. Get system health status (real-time data)
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
                MetricsStartTime = context.MetricsStartTime,
                MetricsEndTime = context.MetricsEndTime
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build dashboard summary");
            return Res.Fail(localizer["Services:Errors:BuildDashboardFailed", ex.Message]);
        }
    }

    /// <summary>
    /// Build a list of recent activities (based on preloaded data, purely in-memory processing)
    /// </summary>
    public Res<List<RecentActivity>> BuildRecentActivities(
        DashboardDataContext context,
        int count = 20)
    {
        try
        {
            // Filter and sort from pre-loaded instances
            var activities = context.AllInstances
                .OrderByDescending(i => i.CompletedAt ?? i.StartedAt ?? i.CreatedAt)
                .Take(count)
                .Select(i => new RecentActivity
                {
                    InstanceId = i.InstanceId,
                    JobKey = i.JobKey,
                    JobName = context.JobNameMap.GetValueOrDefault(i.JobKey, i.JobKey),
                    State = i.State,
                    Timestamp = i.CompletedAt ?? i.StartedAt ?? i.CreatedAt,
                    Duration = i.StartedAt.HasValue && i.CompletedAt.HasValue
                        ? i.CompletedAt.Value - i.StartedAt.Value
                        : null
                })
                .ToList();

            return Res.Ok(activities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build recent activities");
            return Res.Fail(localizer["Services:Errors:BuildRecentActivitiesFailed", ex.Message]);
        }
    }

    /// <summary>
    /// Find problem tasks (based on preloaded data, pure memory processing)
    /// </summary>
    public Res<ProblemJobs> FindProblemJobs(DashboardDataContext context)
    {
        try
        {
            var problemJobs = new ProblemJobs
            {
                ConsecutiveFailures = FindConsecutiveFailures(context),
                LongRunning = FindLongRunningJobs(context),
                HighSkipRate = FindHighSkipRateJobs(context)
            };

            return Res.Ok(problemJobs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find problem jobs");
            return Res.Fail(localizer["Services:Errors:FindProblemJobsFailed", ex.Message]);
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

    /// <summary>
    /// Find consecutive failed tasks (pure memory processing)
    /// </summary>
    private List<ConsecutiveFailureJob> FindConsecutiveFailures(DashboardDataContext context)
    {
        var result = new List<ConsecutiveFailureJob>();
        const int consecutiveThreshold = 3;

        // Group instances by JobKey and sort by CreatedAt descending
        var instancesByJob = context.AllInstances
            .GroupBy(i => i.JobKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).ToList());

        foreach (var definition in context.Definitions.Where(d => !d.IsDeleted && !d.IsDisabled))
        {
            if (!instancesByJob.TryGetValue(definition.JobKey, out var instances))
                continue;

            if (instances.Count < consecutiveThreshold)
                continue;

            var recentInstances = instances.Take(consecutiveThreshold).ToList();
            var allFailed = recentInstances.All(i =>
                i.State == JobState.Failed || i.State == JobState.Terminated);

            if (allFailed)
            {
                var lastFailure = recentInstances.First();
                result.Add(new ConsecutiveFailureJob
                {
                    JobKey = definition.JobKey,
                    JobName = context.JobNameMap.GetValueOrDefault(definition.JobKey, definition.JobKey),
                    ConsecutiveFailureCount = consecutiveThreshold,
                    LastFailureTime = lastFailure.CompletedAt ?? lastFailure.CreatedAt,
                    LastFailureInstanceId = lastFailure.InstanceId
                });
            }
        }

        return result.OrderByDescending(j => j.LastFailureTime).Take(10).ToList();
    }

    /// <summary>
    /// Find long-running tasks (pure memory processing)
    /// </summary>
    private List<LongRunningJob> FindLongRunningJobs(DashboardDataContext context)
    {
        var result = new List<LongRunningJob>();
        var now = DateTime.UtcNow;

        // Filter processing instances from pre-loaded data
        var processingInstances = context.AllInstances
            .Where(i => i.State == JobState.Processing && i.StartedAt.HasValue);

        foreach (var instance in processingInstances)
        {
            var elapsed = now - instance.StartedAt!.Value;
            var maxTimeout = context.JobConfigMap.TryGetValue(instance.JobKey, out var def)
                ? def.MaxExecutionTimeout
                : TimeSpan.FromHours(1);

            // Report if elapsed > 50% of max timeout
            if (elapsed.TotalSeconds > maxTimeout.TotalSeconds * 0.5)
            {
                result.Add(new LongRunningJob
                {
                    InstanceId = instance.InstanceId,
                    JobKey = instance.JobKey,
                    JobName = context.JobNameMap.GetValueOrDefault(instance.JobKey, instance.JobKey),
                    ElapsedTime = elapsed,
                    MaxExecutionTimeout = maxTimeout,
                    StartedAt = instance.StartedAt.Value
                });
            }
        }

        return result.OrderByDescending(j => j.TimeoutPercentage).Take(10).ToList();
    }

    /// <summary>
    /// Find tasks with high skip rates (pure memory processing)
    /// </summary>
    private List<HighSkipRateJob> FindHighSkipRateJobs(DashboardDataContext context)
    {
        var result = new List<HighSkipRateJob>();
        const double skipRateThreshold = 10.0; // 10%

        // Group from pre-loaded instances and calculate skip rate
        var groupedByJob = context.AllInstances
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
                    JobName = context.JobNameMap.GetValueOrDefault(group.JobKey, group.JobKey),
                    SkippedCount = group.SkippedCount,
                    TotalCount = group.TotalCount,
                    MaxConcurrency = context.JobConfigMap.TryGetValue(group.JobKey, out var def)
                        ? def.MaxConcurrency
                        : 1
                });
            }
        }

        return result.OrderByDescending(j => j.SkipRate).Take(10).ToList();
    }
}
