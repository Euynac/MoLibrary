using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 作业统计信息计算服务
/// </summary>
public class JobStatisticsService(
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobStatisticsService> logger)
{
    public async Task<Res<JobStatistics>> CalculateStatisticsAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all instances for this job (no state filter, all time)
            var allInstances = await metadataStore.GetJobInstancesByKeyAsync(
                jobKey,
                stateFilter: null,
                cancellationToken);

            var statistics = new JobStatistics
            {
                JobKey = jobKey,
                TotalExecutions = allInstances.Count,
                FailedExecutions = allInstances.Count(i =>
                    i.State == JobState.Failed || i.State == JobState.Terminated)
            };

            // Calculate execution times for completed jobs
            var completedInstances = allInstances
                .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
                .Select(i => new
                {
                    i.InstanceId,
                    Duration = i.CompletedAt!.Value - i.StartedAt!.Value
                })
                .ToList();

            if (completedInstances.Any())
            {
                statistics.AverageExecutionTime = TimeSpan.FromTicks(
                    (long)completedInstances.Average(i => i.Duration.Ticks));

                var fastest = completedInstances.MinBy(i => i.Duration);
                if (fastest != null)
                {
                    statistics.FastestExecutionTime = fastest.Duration;
                    statistics.FastestInstanceId = fastest.InstanceId;
                }

                var slowest = completedInstances.MaxBy(i => i.Duration);
                if (slowest != null)
                {
                    statistics.SlowestExecutionTime = slowest.Duration;
                    statistics.SlowestInstanceId = slowest.InstanceId;
                }
            }

            // Get last execution
            var lastExecution = allInstances
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefault();

            if (lastExecution != null)
            {
                statistics.LastExecutionTime = lastExecution.CreatedAt;
                statistics.LastExecutionState = lastExecution.State;
            }

            return Res.Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate statistics for {JobKey}", jobKey);
            return Res.Fail($"Failed to calculate statistics: {ex.Message}");
        }
    }
}
