using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.Models;
using Monica.Tool.MoResponse;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// 统计分析服务
/// </summary>
public class JobAnalyticsService(
    IMoJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    ILogger<JobAnalyticsService> logger)
{
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

    #region Optimized Methods with Projection

    /// <summary>
    /// 获取统计分析所需的实例数据（使用投影，不加载 StateHistory 和 JobArgs 等大文本字段）
    /// </summary>
    public async Task<Res<List<JobInstanceStatistics>>> GetInstanceStatisticsAsync(
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

            // Use projection to only load needed fields (database-side SELECT)
            var result = await metadataRepository.QueryInstancesAsync(
                query,
                i => new JobInstanceStatistics
                {
                    InstanceId = i.InstanceId,
                    JobKey = i.JobKey,
                    State = i.State,
                    CreatedAt = i.CreatedAt,
                    StartedAt = i.StartedAt,
                    CompletedAt = i.CompletedAt,
                    RetryAttempt = i.RetryAttempt
                },
                cancellationToken);

            logger.LogDebug(
                "GetInstanceStatisticsAsync: Loaded {Count} projected instances for time range [{Start} - {End}]",
                result.Items.Count, startTime, endTime);

            return Res.Ok(result.Items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instance statistics");
            return Res.Fail($"获取统计数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 计算执行趋势（基于预加载的投影数据）
    /// </summary>
    public Res<List<ExecutionTrendPoint>> CalculateExecutionTrend(
        List<JobInstanceStatistics> instances,
        DateTime startTime,
        DateTime endTime,
        TimeGranularity granularity)
    {
        try
        {
            var groupedData = instances
                .GroupBy(i => GetTimeBucket(i.CreatedAt, granularity))
                .ToDictionary(g => g.Key, g => g.ToList());

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
            logger.LogError(ex, "Failed to calculate execution trend");
            return Res.Fail($"计算执行趋势失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 计算执行耗时百分位数（基于预加载的投影数据）
    /// </summary>
    public Res<DurationPercentiles> CalculateDurationPercentiles(
        List<JobInstanceStatistics> instances,
        string? jobKey = null)
    {
        try
        {
            var filtered = jobKey != null
                ? instances.Where(i => i.JobKey == jobKey)
                : instances;

            var durations = filtered
                .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
                .Where(i => i.State == JobState.Succeeded || i.State == JobState.Failed || i.State == JobState.Terminated)
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
            logger.LogError(ex, "Failed to calculate duration percentiles");
            return Res.Fail($"计算耗时分布失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 计算任务排行榜（基于预加载的投影数据）
    /// </summary>
    public Res<List<JobRanking>> CalculateTopJobs(
        List<JobInstanceStatistics> instances,
        Dictionary<string, string> jobNameMap,
        int topN,
        RankingType rankingType)
    {
        try
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

            var rankedList = ranked.ToList();
            var totalValue = rankedList.Sum(x => x.Value);
            var rankings = rankedList
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
            logger.LogError(ex, "Failed to calculate top jobs");
            return Res.Fail($"计算任务排行榜失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 计算失败分析（基于预加载的投影数据）
    /// </summary>
    public Res<FailureAnalysis> CalculateFailureAnalysis(
        List<JobInstanceStatistics> instances,
        Dictionary<string, string> jobNameMap,
        DateTime startTime,
        DateTime endTime)
    {
        try
        {
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
            logger.LogError(ex, "Failed to calculate failure analysis");
            return Res.Fail($"计算失败分析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 计算最慢的 N 个实例（每个 JobKey 只取最慢的一个）
    /// </summary>
    public Res<List<SlowestInstance>> CalculateSlowestInstances(
        List<JobInstanceStatistics> instances,
        Dictionary<string, string> jobNameMap,
        int topN = 5)
    {
        try
        {
            // Filter completed instances with valid duration
            var completedWithDuration = instances
                .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
                .Where(i => i.State == JobState.Succeeded || i.State == JobState.Failed || i.State == JobState.Terminated)
                .Select(i => new
                {
                    i.InstanceId,
                    i.JobKey,
                    Duration = i.CompletedAt!.Value - i.StartedAt!.Value,
                    CompletedAt = i.CompletedAt.Value
                })
                .ToList();

            // Group by JobKey and take the slowest instance for each
            var slowestByJob = completedWithDuration
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

            return Res.Ok(slowestByJob);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate slowest instances");
            return Res.Fail($"计算最慢实例失败: {ex.Message}");
        }
    }

    #endregion
}
