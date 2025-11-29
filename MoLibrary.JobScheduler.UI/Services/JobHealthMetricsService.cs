using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.JobScheduler.UI.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 作业健康指标服务（基于可配置的时间窗口）
/// </summary>
public class JobHealthMetricsService(
    IMoJobScheduleMetadataStore metadataStore,
    IOptions<ModuleJobSchedulerUIOption> uiOptions,
    ILogger<JobHealthMetricsService> logger)
{
    private readonly ModuleJobSchedulerUIOption _options = uiOptions.Value;

    public async Task<Res<JobHealthMetrics>> CalculateHealthMetricsAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - _options.HealthMetricsWindow;

            // Get all instances in time window
            var instances = await metadataStore.GetJobInstancesAsync(
                jobKey: jobKey,
                stateFilter: null,
                startTime: startTime,
                endTime: endTime,
                pageNumber: 1,
                pageSize: 10000, // Get all within window
                cancellationToken: cancellationToken);

            // Group instances by state for efficient counting
            var stateGroups = instances.GroupBy(i => i.State)
                .ToDictionary(g => g.Key, g => g.Count());

            var failedInstances = instances
                .Where(i => i.State == JobState.Failed || i.State == JobState.Terminated)
                .OrderByDescending(i => i.CreatedAt)
                .Take(_options.HealthMetricsFailedInstancesLimit)
                .ToList();

            var metrics = new JobHealthMetrics
            {
                JobKey = jobKey,
                TotalExecutions = instances.Count,

                // State counts
                SucceededCount = stateGroups.GetValueOrDefault(JobState.Succeeded, 0),
                SkippedCount = stateGroups.GetValueOrDefault(JobState.Skipped, 0),
                CancelledCount = stateGroups.GetValueOrDefault(JobState.Cancelled, 0),
                ProcessingCount = stateGroups.GetValueOrDefault(JobState.Processing, 0),
                FailedCount = stateGroups.GetValueOrDefault(JobState.Failed, 0),
                TerminatedCount = stateGroups.GetValueOrDefault(JobState.Terminated, 0),
                EnqueuedCount = stateGroups.GetValueOrDefault(JobState.Enqueued, 0),
                ScheduledCount = stateGroups.GetValueOrDefault(JobState.Scheduled, 0),
                RecentFailures = failedInstances,
                MetricsStartTime = startTime,
                MetricsEndTime = endTime
            };

            return Res.Ok(metrics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate health metrics for {JobKey}", jobKey);
            return Res.Fail($"Failed to calculate health metrics: {ex.Message}");
        }
    }
}
