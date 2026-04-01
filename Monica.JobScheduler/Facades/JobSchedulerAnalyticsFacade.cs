using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Facades;

/// <summary>
/// Analytics-oriented entry points for Job Scheduler reporting UIs.
/// </summary>
public class JobSchedulerAnalyticsFacade(
    IJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    ILogger<JobSchedulerAnalyticsFacade> logger)
{
    /// <summary>
    /// Builds the analytics snapshot for the requested time range.
    /// </summary>
    public async Task<Res<JobAnalyticsSnapshot>> GetAnalyticsAsync(
        DateTime startTime,
        DateTime endTime,
        TimeGranularity granularity,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instances = await LoadInstanceStatisticsAsync(startTime, endTime, cancellationToken);
            var jobNameMap = (await cacheService.GetAllDefinitionsAsync(cancellationToken))
                .ToDictionary(d => d.JobKey, d => d.JobName);

            return Res.Ok(new JobAnalyticsSnapshot
            {
                TrendData = CalculateExecutionTrend(instances, startTime, endTime, granularity),
                Percentiles = CalculateDurationPercentiles(instances),
                SlowestInstances = CalculateSlowestInstances(instances, jobNameMap, topN),
                TopByExecution = CalculateTopJobs(instances, jobNameMap, topN, RankingType.ExecutionCount),
                TopByFailure = CalculateTopJobs(instances, jobNameMap, topN, RankingType.FailureCount),
                FailureAnalysis = CalculateFailureAnalysis(instances, jobNameMap, startTime, endTime),
                StartTime = startTime,
                EndTime = endTime,
                Granularity = granularity
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build analytics snapshot");
            return Res.Fail($"Failed to build analytics snapshot: {ex.GetMessageRecursively()}");
        }
    }

    private async Task<List<JobInstanceStatistics>> LoadInstanceStatisticsAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var query = new JobInstanceQuery
        {
            CreatedAfter = startTime,
            CreatedBefore = endTime,
            PageNumber = 1,
            PageSize = int.MaxValue
        };

        var result = await metadataRepository.QueryInstancesAsync(
            query,
            instance => new JobInstanceStatistics
            {
                InstanceId = instance.InstanceId,
                JobKey = instance.JobKey,
                State = instance.State,
                CreatedAt = instance.CreatedAt,
                StartedAt = instance.StartedAt,
                CompletedAt = instance.CompletedAt,
                RetryAttempt = instance.RetryAttempt
            },
            cancellationToken);

        return result.Items;
    }

    private static List<ExecutionTrendPoint> CalculateExecutionTrend(
        List<JobInstanceStatistics> instances,
        DateTime startTime,
        DateTime endTime,
        TimeGranularity granularity)
    {
        var groupedData = instances
            .GroupBy(i => GetTimeBucket(i.CreatedAt, granularity))
            .ToDictionary(g => g.Key, g => g.ToList());

        return GenerateTimePoints(startTime, endTime, granularity)
            .Select(timestamp =>
            {
                var bucket = groupedData.GetValueOrDefault(timestamp, []);
                return new ExecutionTrendPoint
                {
                    Timestamp = timestamp,
                    SucceededCount = bucket.Count(i => i.State == JobState.Succeeded),
                    FailedCount = bucket.Count(i => i.State is JobState.Failed or JobState.Terminated),
                    SkippedCount = bucket.Count(i => i.State == JobState.Skipped),
                    CancelledCount = bucket.Count(i => i.State == JobState.Cancelled),
                    TotalCount = bucket.Count
                };
            })
            .ToList();
    }

    private static DurationPercentiles CalculateDurationPercentiles(
        List<JobInstanceStatistics> instances,
        string? jobKey = null)
    {
        var filtered = jobKey is null
            ? instances
            : instances.Where(i => i.JobKey == jobKey);
        var durations = filtered
            .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
            .Where(i => i.State is JobState.Succeeded or JobState.Failed or JobState.Terminated)
            .Select(i => i.CompletedAt!.Value - i.StartedAt!.Value)
            .OrderBy(d => d)
            .ToList();

        if (durations.Count == 0)
        {
            return new DurationPercentiles
            {
                JobKey = jobKey,
                SampleCount = 0
            };
        }

        return new DurationPercentiles
        {
            JobKey = jobKey,
            P50 = GetPercentile(durations, 50),
            P90 = GetPercentile(durations, 90),
            P95 = GetPercentile(durations, 95),
            P99 = GetPercentile(durations, 99),
            Average = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks)),
            Min = durations.First(),
            Max = durations.Last(),
            SampleCount = durations.Count
        };
    }

    private static List<JobRanking> CalculateTopJobs(
        List<JobInstanceStatistics> instances,
        Dictionary<string, string> jobNameMap,
        int topN,
        RankingType rankingType)
    {
        var groupedByJob = instances.GroupBy(i => i.JobKey).ToList();

        IEnumerable<(string JobKey, double Value, string FormattedValue)> ranked = rankingType switch
        {
            RankingType.ExecutionCount => groupedByJob
                .Select(g => (g.Key, (double)g.Count(), g.Count().ToString()))
                .OrderByDescending(x => x.Item2),

            RankingType.FailureCount => groupedByJob
                .Select(g =>
                {
                    var failCount = g.Count(i => i.State is JobState.Failed or JobState.Terminated);
                    return (g.Key, (double)failCount, failCount.ToString());
                })
                .OrderByDescending(x => x.Item2),

            RankingType.AverageDuration => groupedByJob
                .Select(g =>
                {
                    var durations = g
                        .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
                        .Select(i => i.CompletedAt!.Value - i.StartedAt!.Value)
                        .ToList();
                    var averageMilliseconds = durations.Count > 0
                        ? durations.Average(duration => duration.TotalMilliseconds)
                        : 0;
                    return (g.Key, averageMilliseconds, FormatDuration(TimeSpan.FromMilliseconds(averageMilliseconds)));
                })
                .OrderByDescending(x => x.Item2),

            RankingType.SkipCount => groupedByJob
                .Select(g =>
                {
                    var skipCount = g.Count(i => i.State == JobState.Skipped);
                    return (g.Key, (double)skipCount, skipCount.ToString());
                })
                .OrderByDescending(x => x.Item2),

            _ => throw new ArgumentOutOfRangeException(nameof(rankingType), rankingType, null)
        };

        var rankedList = ranked.ToList();
        var totalValue = rankedList.Sum(x => x.Value);

        return rankedList
            .Take(topN)
            .Select((item, index) => new JobRanking
            {
                Rank = index + 1,
                JobKey = item.JobKey,
                JobName = jobNameMap.GetValueOrDefault(item.JobKey, item.JobKey),
                Value = item.Value,
                FormattedValue = item.FormattedValue,
                Percentage = totalValue > 0 ? item.Value / totalValue * 100 : 0
            })
            .ToList();
    }

    private static FailureAnalysis CalculateFailureAnalysis(
        List<JobInstanceStatistics> instances,
        Dictionary<string, string> jobNameMap,
        DateTime startTime,
        DateTime endTime)
    {
        var byJob = instances
            .GroupBy(i => i.JobKey)
            .Select(g => new JobFailureCount
            {
                JobKey = g.Key,
                JobName = jobNameMap.GetValueOrDefault(g.Key, g.Key),
                FailedCount = g.Count(i => i.State == JobState.Failed),
                TerminatedCount = g.Count(i => i.State == JobState.Terminated),
                TotalExecutions = g.Count()
            })
            .Where(item => item.TotalFailureCount > 0)
            .OrderByDescending(item => item.TotalFailureCount)
            .Take(20)
            .ToList();

        var retriedInstances = instances.Where(i => i.RetryAttempt > 0).ToList();
        var retriedAndSucceeded = retriedInstances.Count(i => i.State == JobState.Succeeded);
        var retrySuccessRate = retriedInstances.Count > 0
            ? (double)retriedAndSucceeded / retriedInstances.Count * 100
            : 100;

        return new FailureAnalysis
        {
            ByJob = byJob,
            RetrySuccessRate = Math.Round(retrySuccessRate, 1),
            TotalFailures = instances.Count(i => i.State == JobState.Failed),
            TotalTerminated = instances.Count(i => i.State == JobState.Terminated),
            StartTime = startTime,
            EndTime = endTime
        };
    }

    private static List<SlowestInstance> CalculateSlowestInstances(
        List<JobInstanceStatistics> instances,
        Dictionary<string, string> jobNameMap,
        int topN)
    {
        return instances
            .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
            .Where(i => i.State is JobState.Succeeded or JobState.Failed or JobState.Terminated)
            .Select(i => new
            {
                i.InstanceId,
                i.JobKey,
                Duration = i.CompletedAt!.Value - i.StartedAt!.Value,
                CompletedAt = i.CompletedAt.Value
            })
            .GroupBy(i => i.JobKey)
            .Select(g => g.OrderByDescending(i => i.Duration).First())
            .OrderByDescending(i => i.Duration)
            .Take(topN)
            .Select(i => new SlowestInstance
            {
                InstanceId = i.InstanceId,
                JobKey = i.JobKey,
                JobName = jobNameMap.GetValueOrDefault(i.JobKey, i.JobKey),
                Duration = i.Duration,
                CompletedAt = i.CompletedAt
            })
            .ToList();
    }

    private static DateTime GetTimeBucket(DateTime timestamp, TimeGranularity granularity)
    {
        return granularity switch
        {
            TimeGranularity.Hourly => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0, DateTimeKind.Utc),
            TimeGranularity.Daily => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0, DateTimeKind.Utc),
            TimeGranularity.Weekly => timestamp.Date.AddDays(-(int)timestamp.DayOfWeek),
            _ => timestamp
        };
    }

    private static IEnumerable<DateTime> GenerateTimePoints(
        DateTime startTime,
        DateTime endTime,
        TimeGranularity granularity)
    {
        var current = GetTimeBucket(startTime, granularity);
        var end = GetTimeBucket(endTime, granularity);

        while (current <= end)
        {
            yield return current;
            current = granularity switch
            {
                TimeGranularity.Hourly => current.AddHours(1),
                TimeGranularity.Daily => current.AddDays(1),
                TimeGranularity.Weekly => current.AddDays(7),
                _ => current.AddHours(1)
            };
        }
    }

    private static TimeSpan GetPercentile(List<TimeSpan> sortedDurations, int percentile)
    {
        if (sortedDurations.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var index = (int)Math.Ceiling(percentile / 100.0 * sortedDurations.Count) - 1;
        index = Math.Max(0, Math.Min(index, sortedDurations.Count - 1));
        return sortedDurations[index];
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{duration.TotalDays:F1}d";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{duration.TotalHours:F1}h";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.TotalMinutes:F1}m";
        }

        if (duration.TotalSeconds >= 1)
        {
            return $"{duration.TotalSeconds:F1}s";
        }

        return $"{duration.TotalMilliseconds:F0}ms";
    }
}
