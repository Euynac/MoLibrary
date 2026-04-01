using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Facades;

/// <summary>
/// Dashboard-oriented entry points for Job Scheduler monitoring UIs.
/// </summary>
public class JobSchedulerDashboardFacade(
    IJobDefinitionCacheService cacheService,
    IJobMetadataRepository metadataRepository,
    IJobConcurrencyGuard concurrencyGuard,
    HealthCheckService healthCheckService,
    ILogger<JobSchedulerDashboardFacade> logger)
{
    /// <summary>
    /// Builds the full dashboard snapshot for the requested metrics window.
    /// </summary>
    public async Task<Res<JobDashboardSnapshot>> GetDashboardAsync(
        TimeSpan metricsWindow,
        int recentActivityCount = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var metricsStartTime = now - metricsWindow;
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);
            var jobConfigMap = definitions.ToDictionary(d => d.JobKey, d => d);

            var stateDistributionTask = metadataRepository.GetStateStatisticsAsync(
                metricsStartTime,
                now,
                cancellationToken);
            var instancesTask = LoadInstancesAsync(metricsStartTime, cancellationToken);
            var executionStatsTask = concurrencyGuard.GetAllExecutionStatisticsAsync(cancellationToken);
            var healthTask = GetSystemHealthStatusAsync(cancellationToken);

            await Task.WhenAll(stateDistributionTask, instancesTask, executionStatsTask, healthTask);

            var stateDistribution = await stateDistributionTask;
            var instances = await instancesTask;
            var executionStats = await executionStatsTask;
            var (healthStatus, healthMessage) = await healthTask;

            var summary = BuildSummary(
                definitions,
                stateDistribution,
                executionStats,
                metricsStartTime,
                now,
                healthStatus,
                healthMessage);

            return Res.Ok(new JobDashboardSnapshot
            {
                Summary = summary,
                RecentActivities = BuildRecentActivities(instances, jobNameMap, recentActivityCount),
                Problems = BuildProblems(definitions, instances, jobNameMap, jobConfigMap)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build dashboard snapshot");
            return Res.Fail($"Failed to build dashboard snapshot: {ex.GetMessageRecursively()}");
        }
    }

    private static DashboardSummary BuildSummary(
        IReadOnlyList<JobDefinition> definitions,
        IReadOnlyDictionary<JobState, int> stateDistribution,
        IReadOnlyDictionary<string, JobExecutionStatisticSnapshot> executionStats,
        DateTime metricsStartTime,
        DateTime metricsEndTime,
        SystemHealthStatus healthStatus,
        string? healthMessage)
    {
        var recurringCount = definitions.Count(d => d.JobType == JobType.Recurring && !d.IsDeleted);
        var triggeredCount = definitions.Count(d => d.JobType == JobType.Triggered && !d.IsDeleted);
        var disabledCount = definitions.Count(d => d.IsDisabled && !d.IsDeleted);
        var totalJobs = definitions.Count(d => !d.IsDeleted);
        var runningNow = executionStats.Values.Sum(s => s.RunningCount);
        var terminalStates = new[]
        {
            JobState.Succeeded,
            JobState.Failed,
            JobState.Terminated,
            JobState.Cancelled,
            JobState.Skipped
        };
        var completedCount = terminalStates.Sum(state => stateDistribution.GetValueOrDefault(state, 0));
        var succeededCount = stateDistribution.GetValueOrDefault(JobState.Succeeded, 0);
        var successRate = completedCount > 0 ? ((double)succeededCount / completedCount) * 100 : 100;
        var hoursInWindow = (metricsEndTime - metricsStartTime).TotalHours;
        var throughputPerHour = hoursInWindow > 0
            ? (int)Math.Round(completedCount / hoursInWindow)
            : 0;

        return new DashboardSummary
        {
            TotalJobs = totalJobs,
            RecurringJobCount = recurringCount,
            TriggeredJobCount = triggeredCount,
            DisabledJobCount = disabledCount,
            RunningNow = runningNow,
            SuccessRate = Math.Round(successRate, 1),
            ThroughputPerHour = throughputPerHour,
            StateDistribution = stateDistribution.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            HealthStatus = healthStatus,
            HealthMessage = healthMessage,
            MetricsStartTime = metricsStartTime,
            MetricsEndTime = metricsEndTime
        };
    }

    private static IReadOnlyList<RecentActivity> BuildRecentActivities(
        IReadOnlyList<DashboardInstanceProjection> instances,
        IReadOnlyDictionary<string, string> jobNameMap,
        int count)
    {
        return instances
            .OrderByDescending(i => i.CompletedAt ?? i.StartedAt ?? i.CreatedAt)
            .Take(count)
            .Select(i => new RecentActivity
            {
                InstanceId = i.InstanceId,
                JobKey = i.JobKey,
                JobName = jobNameMap.GetValueOrDefault(i.JobKey, i.JobKey),
                State = i.State,
                Timestamp = i.CompletedAt ?? i.StartedAt ?? i.CreatedAt,
                Duration = i.StartedAt.HasValue && i.CompletedAt.HasValue
                    ? i.CompletedAt.Value - i.StartedAt.Value
                    : null
            })
            .ToList();
    }

    private static ProblemJobs BuildProblems(
        IReadOnlyList<JobDefinition> definitions,
        IReadOnlyList<DashboardInstanceProjection> instances,
        IReadOnlyDictionary<string, string> jobNameMap,
        IReadOnlyDictionary<string, JobDefinition> jobConfigMap)
    {
        return new ProblemJobs
        {
            ConsecutiveFailures = FindConsecutiveFailures(definitions, instances, jobNameMap),
            LongRunning = FindLongRunningJobs(instances, jobNameMap, jobConfigMap),
            HighSkipRate = FindHighSkipRateJobs(instances, jobNameMap, jobConfigMap)
        };
    }

    private static List<ConsecutiveFailureJob> FindConsecutiveFailures(
        IReadOnlyList<JobDefinition> definitions,
        IReadOnlyList<DashboardInstanceProjection> instances,
        IReadOnlyDictionary<string, string> jobNameMap)
    {
        var result = new List<ConsecutiveFailureJob>();
        const int consecutiveThreshold = 3;
        var instancesByJob = instances
            .GroupBy(i => i.JobKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).ToList());

        foreach (var definition in definitions.Where(d => !d.IsDeleted && !d.IsDisabled))
        {
            if (!instancesByJob.TryGetValue(definition.JobKey, out var jobInstances) || jobInstances.Count < consecutiveThreshold)
            {
                continue;
            }

            var recentInstances = jobInstances.Take(consecutiveThreshold).ToList();
            if (recentInstances.All(i => i.State is JobState.Failed or JobState.Terminated))
            {
                var lastFailure = recentInstances[0];
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

    private static List<LongRunningJob> FindLongRunningJobs(
        IReadOnlyList<DashboardInstanceProjection> instances,
        IReadOnlyDictionary<string, string> jobNameMap,
        IReadOnlyDictionary<string, JobDefinition> jobConfigMap)
    {
        var now = DateTime.UtcNow;
        var result = new List<LongRunningJob>();

        foreach (var instance in instances.Where(i => i.State == JobState.Processing && i.StartedAt.HasValue))
        {
            var elapsed = now - instance.StartedAt!.Value;
            var maxTimeout = jobConfigMap.TryGetValue(instance.JobKey, out var definition)
                ? definition.MaxExecutionTimeout
                : TimeSpan.FromHours(1);

            if (elapsed.TotalSeconds <= maxTimeout.TotalSeconds * 0.5)
            {
                continue;
            }

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

        return result.OrderByDescending(j => j.TimeoutPercentage).Take(10).ToList();
    }

    private static List<HighSkipRateJob> FindHighSkipRateJobs(
        IReadOnlyList<DashboardInstanceProjection> instances,
        IReadOnlyDictionary<string, string> jobNameMap,
        IReadOnlyDictionary<string, JobDefinition> jobConfigMap)
    {
        const double skipRateThreshold = 10;
        var result = new List<HighSkipRateJob>();
        var groupedByJob = instances
            .GroupBy(i => i.JobKey)
            .Select(g => new
            {
                JobKey = g.Key,
                TotalCount = g.Count(),
                SkippedCount = g.Count(i => i.State == JobState.Skipped)
            })
            .Where(g => g.TotalCount >= 10 && g.SkippedCount > 0);

        foreach (var group in groupedByJob)
        {
            var skipRate = (double)group.SkippedCount / group.TotalCount * 100;
            if (skipRate < skipRateThreshold)
            {
                continue;
            }

            result.Add(new HighSkipRateJob
            {
                JobKey = group.JobKey,
                JobName = jobNameMap.GetValueOrDefault(group.JobKey, group.JobKey),
                SkippedCount = group.SkippedCount,
                TotalCount = group.TotalCount,
                MaxConcurrency = jobConfigMap.TryGetValue(group.JobKey, out var definition)
                    ? definition.MaxConcurrency
                    : 1
            });
        }

        return result.OrderByDescending(j => j.SkipRate).Take(10).ToList();
    }

    private async Task<(SystemHealthStatus Status, string? Message)> GetSystemHealthStatusAsync(
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
            logger.LogWarning(ex, "Failed to get job scheduler health status");
            return (SystemHealthStatus.Healthy, null);
        }
    }

    private async Task<List<DashboardInstanceProjection>> LoadInstancesAsync(
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        var query = new JobInstanceQuery
        {
            CreatedAfter = startTime,
            PageNumber = 1,
            PageSize = int.MaxValue,
            SortBy = "CreatedAt",
            SortDescending = true
        };

        var result = await metadataRepository.QueryInstancesAsync(
            query,
            i => new DashboardInstanceProjection(
                i.InstanceId,
                i.JobKey,
                i.State,
                i.CreatedAt,
                i.StartedAt,
                i.CompletedAt),
            cancellationToken);

        return result.Items;
    }

    private sealed record DashboardInstanceProjection(
        string InstanceId,
        string JobKey,
        JobState State,
        DateTime CreatedAt,
        DateTime? StartedAt,
        DateTime? CompletedAt);
}
