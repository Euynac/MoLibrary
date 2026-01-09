using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 统计分析服务
/// </summary>
public class JobAnalyticsService(
    IMoJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    ILogger<JobAnalyticsService> logger)
{
    /// <summary>
    /// 获取执行趋势数据
    /// </summary>
    public async Task<Res<List<ExecutionTrendPoint>>> GetExecutionTrendAsync(
        DateTime startTime,
        DateTime endTime,
        TimeGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all instances in time range
            var query = new JobInstanceQuery
            {
                CreatedAfter = startTime,
                CreatedBefore = endTime,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            var instances = result.Items;

            // Group by time bucket
            var groupedData = GroupByTimeBucket(instances, granularity);

            // Generate all time points in range
            var trendPoints = GenerateTimePoints(startTime, endTime, granularity)
                .Select(timestamp =>
                {
                    var bucket = groupedData.GetValueOrDefault(timestamp, []);
                    return new ExecutionTrendPoint
                    {
                        Timestamp = timestamp,
                        SucceededCount = bucket.Count(i => i.State == JobState.Succeeded),
                        FailedCount = bucket.Count(i => i.State == JobState.Failed || i.State == JobState.Terminated),
                        SkippedCount = bucket.Count(i => i.State == JobState.Skipped),
                        CancelledCount = bucket.Count(i => i.State == JobState.Cancelled),
                        TotalCount = bucket.Count
                    };
                })
                .ToList();

            return Res.Ok(trendPoints);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get execution trend");
            return Res.Fail($"获取执行趋势失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取执行耗时百分位数
    /// </summary>
    public async Task<Res<DurationPercentiles>> GetDurationPercentilesAsync(
        DateTime startTime,
        DateTime endTime,
        string? jobKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new JobInstanceQuery
            {
                JobKey = jobKey,
                CreatedAfter = startTime,
                CreatedBefore = endTime,
                States = [JobState.Succeeded, JobState.Failed, JobState.Terminated],
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

            // Filter instances with valid duration
            var durations = result.Items
                .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
                .Select(i => i.CompletedAt!.Value - i.StartedAt!.Value)
                .OrderBy(d => d)
                .ToList();

            if (durations.Count == 0)
            {
                return Res.Ok(new DurationPercentiles
                {
                    JobKey = jobKey,
                    SampleCount = 0
                });
            }

            return Res.Ok(new DurationPercentiles
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get duration percentiles");
            return Res.Fail($"获取耗时分布失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取任务排行榜
    /// </summary>
    public async Task<Res<List<JobRanking>>> GetTopJobsAsync(
        DateTime startTime,
        DateTime endTime,
        int topN,
        RankingType rankingType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new JobInstanceQuery
            {
                CreatedAfter = startTime,
                CreatedBefore = endTime,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            var instances = result.Items;

            // Get job definitions for names
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);

            var groupedByJob = instances.GroupBy(i => i.JobKey).ToList();

            IEnumerable<(string JobKey, double Value, string FormattedValue)> ranked = rankingType switch
            {
                RankingType.ExecutionCount => groupedByJob
                    .Select(g => (g.Key, (double)g.Count(), g.Count().ToString()))
                    .OrderByDescending(x => x.Item2),

                RankingType.FailureCount => groupedByJob
                    .Select(g =>
                    {
                        var failCount = g.Count(i => i.State == JobState.Failed || i.State == JobState.Terminated);
                        return (g.Key, (double)failCount, failCount.ToString());
                    })
                    .OrderByDescending(x => x.Item2),

                RankingType.AverageDuration => groupedByJob
                    .Select(g =>
                    {
                        var completedWithDuration = g
                            .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
                            .Select(i => i.CompletedAt!.Value - i.StartedAt!.Value)
                            .ToList();
                        var avgMs = completedWithDuration.Count > 0
                            ? completedWithDuration.Average(d => d.TotalMilliseconds)
                            : 0;
                        var avgTimeSpan = TimeSpan.FromMilliseconds(avgMs);
                        return (g.Key, avgMs, FormatDuration(avgTimeSpan));
                    })
                    .OrderByDescending(x => x.Item2),

                RankingType.SkipCount => groupedByJob
                    .Select(g =>
                    {
                        var skipCount = g.Count(i => i.State == JobState.Skipped);
                        return (g.Key, (double)skipCount, skipCount.ToString());
                    })
                    .OrderByDescending(x => x.Item2),

                _ => throw new ArgumentOutOfRangeException(nameof(rankingType))
            };

            var totalValue = ranked.Sum(x => x.Value);
            var rankings = ranked
                .Take(topN)
                .Select((item, index) => new JobRanking
                {
                    Rank = index + 1,
                    JobKey = item.JobKey,
                    JobName = jobNameMap.GetValueOrDefault(item.JobKey, item.JobKey),
                    Value = item.Value,
                    FormattedValue = item.FormattedValue,
                    Percentage = totalValue > 0 ? (item.Value / totalValue) * 100 : 0
                })
                .ToList();

            return Res.Ok(rankings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get top jobs");
            return Res.Fail($"获取任务排行榜失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取失败分析
    /// </summary>
    public async Task<Res<FailureAnalysis>> GetFailureAnalysisAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new JobInstanceQuery
            {
                CreatedAfter = startTime,
                CreatedBefore = endTime,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            var instances = result.Items;

            // Get job definitions for names
            var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);

            // Group by job and calculate failure counts
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
                .Where(j => j.TotalFailureCount > 0)
                .OrderByDescending(j => j.TotalFailureCount)
                .Take(20)
                .ToList();

            // Calculate retry success rate
            // A retry is considered successful if an instance with RetryAttempt > 0 succeeded
            var retriedInstances = instances.Where(i => i.RetryAttempt > 0).ToList();
            var retriedAndSucceeded = retriedInstances.Count(i => i.State == JobState.Succeeded);
            var retrySuccessRate = retriedInstances.Count > 0
                ? ((double)retriedAndSucceeded / retriedInstances.Count) * 100
                : 100;

            var totalFailures = instances.Count(i => i.State == JobState.Failed);
            var totalTerminated = instances.Count(i => i.State == JobState.Terminated);

            return Res.Ok(new FailureAnalysis
            {
                ByJob = byJob,
                RetrySuccessRate = Math.Round(retrySuccessRate, 1),
                TotalFailures = totalFailures,
                TotalTerminated = totalTerminated,
                StartTime = startTime,
                EndTime = endTime
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get failure analysis");
            return Res.Fail($"获取失败分析失败: {ex.Message}");
        }
    }

    private static Dictionary<DateTime, List<JobInstance>> GroupByTimeBucket(
        List<JobInstance> instances,
        TimeGranularity granularity)
    {
        return instances
            .GroupBy(i => GetTimeBucket(i.CreatedAt, granularity))
            .ToDictionary(g => g.Key, g => g.ToList());
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
            return TimeSpan.Zero;

        var index = (int)Math.Ceiling(percentile / 100.0 * sortedDurations.Count) - 1;
        index = Math.Max(0, Math.Min(index, sortedDurations.Count - 1));
        return sortedDurations[index];
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:F1}d";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1}h";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1}m";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:F1}s";
        return $"{duration.TotalMilliseconds:F0}ms";
    }
}
