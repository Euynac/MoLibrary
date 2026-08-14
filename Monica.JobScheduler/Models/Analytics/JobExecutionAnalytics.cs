using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.Models.Analytics;

/// <summary>
/// Selects the fixed UTC interval used to aggregate an execution analytics trend.
/// </summary>
public enum JobExecutionAnalyticsBucketSize
{
    /// <summary>
    /// Aggregates completions into consecutive one-hour intervals.
    /// </summary>
    Hour,

    /// <summary>
    /// Aggregates completions into consecutive 24-hour intervals.
    /// </summary>
    Day
}

/// <summary>
/// Defines one bounded scheduler analytics query.
/// </summary>
/// <remarks>
/// The range uses an inclusive start and exclusive end. Hourly queries may cover at most 31 days and daily queries
/// may cover at most 366 days, which keeps every returned trend finite even when the durable execution history is
/// much larger. All aggregation intervals are fixed UTC intervals; they do not change length across daylight-saving
/// transitions.
/// </remarks>
public sealed record JobExecutionAnalyticsQuery
{
    /// <summary>
    /// Gets the maximum number of jobs returned by each ranking.
    /// </summary>
    public const int MAX_TOP_JOB_COUNT = 20;

    /// <summary>
    /// Gets the maximum number of slow executions returned by one query.
    /// </summary>
    public const int MAX_SLOWEST_EXECUTION_COUNT = 50;

    /// <summary>
    /// Gets the inclusive UTC range start.
    /// </summary>
    public required DateTimeOffset StartTimeUtc { get; init; }

    /// <summary>
    /// Gets the exclusive UTC range end.
    /// </summary>
    public required DateTimeOffset EndTimeUtc { get; init; }

    /// <summary>
    /// Gets the fixed UTC trend interval.
    /// </summary>
    public JobExecutionAnalyticsBucketSize BucketSize { get; init; } = JobExecutionAnalyticsBucketSize.Hour;

    /// <summary>
    /// Gets an optional exact logical job key. When omitted, the complete scheduler scope is aggregated.
    /// </summary>
    public string? JobKey { get; init; }

    /// <summary>
    /// Gets the maximum number of entries in each volume and failure ranking.
    /// </summary>
    public int TopJobLimit { get; init; } = 8;

    /// <summary>
    /// Gets the maximum number of distinct jobs represented by their longest terminal attempt.
    /// </summary>
    public int SlowestExecutionLimit { get; init; } = 10;

    internal JobExecutionAnalyticsRange ValidateAndNormalize()
    {
        if (!Enum.IsDefined(BucketSize))
        {
            throw new ArgumentOutOfRangeException(nameof(BucketSize), BucketSize, "Analytics bucket size is not supported.");
        }

        if (JobKey is not null)
        {
            JobSchedulerIdentity.ValidateJobKey(JobKey, nameof(JobKey));
        }

        if (TopJobLimit is < 1 or > MAX_TOP_JOB_COUNT)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TopJobLimit),
                TopJobLimit,
                $"Top-job limit must be between 1 and {MAX_TOP_JOB_COUNT}.");
        }

        if (SlowestExecutionLimit is < 1 or > MAX_SLOWEST_EXECUTION_COUNT)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SlowestExecutionLimit),
                SlowestExecutionLimit,
                $"Slowest-execution limit must be between 1 and {MAX_SLOWEST_EXECUTION_COUNT}.");
        }

        var start = StartTimeUtc.ToUniversalTime();
        var end = EndTimeUtc.ToUniversalTime();
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(EndTimeUtc), EndTimeUtc, "Analytics range end must be after its start.");
        }

        var bucketDuration = BucketSize == JobExecutionAnalyticsBucketSize.Hour
            ? TimeSpan.FromHours(1)
            : TimeSpan.FromDays(1);
        var maxDuration = BucketSize == JobExecutionAnalyticsBucketSize.Hour
            ? TimeSpan.FromDays(31)
            : TimeSpan.FromDays(366);
        var duration = end - start;
        if (duration > maxDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EndTimeUtc),
                EndTimeUtc,
                $"{BucketSize} analytics may cover at most {maxDuration.TotalDays:0} days.");
        }

        var bucketCount = checked((int)Math.Ceiling(duration.Ticks / (double)bucketDuration.Ticks));
        return new JobExecutionAnalyticsRange(start, end, bucketDuration, bucketCount);
    }
}

/// <summary>
/// Contains bounded execution analytics for one scheduler scope and time range.
/// </summary>
/// <remarks>
/// <see cref="StateTotals"/> describes the cohort created in the requested range, including work that remains queued
/// or running. Reliability, recurring-schedule fulfillment, throughput, trend, duration, ranking, and slow-execution
/// metrics instead describe executions completed in that range. This deliberate split answers both “what entered the
/// scheduler?” and “what work finished?” without excluding long-running work that began before the range.
/// </remarks>
public sealed record JobExecutionAnalyticsSnapshot
{
    /// <summary>
    /// Gets the inclusive normalized UTC range start.
    /// </summary>
    public DateTimeOffset StartTimeUtc { get; init; }

    /// <summary>
    /// Gets the exclusive normalized UTC range end.
    /// </summary>
    public DateTimeOffset EndTimeUtc { get; init; }

    /// <summary>
    /// Gets the trend interval selected by the query.
    /// </summary>
    public JobExecutionAnalyticsBucketSize BucketSize { get; init; }

    /// <summary>
    /// Gets the optional exact job key selected by the query.
    /// </summary>
    public string? JobKey { get; init; }

    /// <summary>
    /// Gets counts by current state for executions created in the requested range. Every defined state is present.
    /// </summary>
    public required IReadOnlyDictionary<JobExecutionState, long> StateTotals { get; init; }

    /// <summary>
    /// Gets the number of all terminal dispositions completed in the range, including skipped and cancelled work.
    /// </summary>
    public long CompletedTerminalCount { get; init; }

    /// <summary>
    /// Gets the number of terminal executions that started an attempt and completed in the range.
    /// </summary>
    public long ExecutedTerminalCount { get; init; }

    /// <summary>
    /// Gets executed terminal completions per hour across the requested range.
    /// </summary>
    public double ExecutedThroughputPerHour { get; init; }

    /// <summary>
    /// Gets successful completions divided by successful plus failed completions. Returns zero with no decided runs.
    /// </summary>
    public double Reliability { get; init; }

    /// <summary>
    /// Gets the number of completed recurring-schedule occurrences whose terminal state is succeeded, failed, or
    /// skipped. Triggered executions, operator run-now executions, and cancelled schedule occurrences are excluded.
    /// </summary>
    public long RecurringScheduleDispositionCount { get; init; }

    /// <summary>
    /// Gets the fraction of completed recurring-schedule dispositions that entered execution instead of being skipped.
    /// Successful and failed scheduled executions both count as fulfilled because the scheduler delivered the
    /// occurrence to a worker. Returns zero when <see cref="RecurringScheduleDispositionCount"/> is zero. Triggered
    /// executions, operator run-now executions, and cancelled schedule occurrences are excluded.
    /// </summary>
    public double RecurringScheduleFulfillment { get; init; }

    /// <summary>
    /// Gets terminal-attempt duration statistics for executions completed in the range.
    /// </summary>
    public required JobExecutionDurationStatistics Duration { get; init; }

    /// <summary>
    /// Gets every consecutive trend bucket, including empty buckets.
    /// </summary>
    public required IReadOnlyList<JobExecutionAnalyticsBucket> Trend { get; init; }

    /// <summary>
    /// Gets the bounded terminal-volume ranking.
    /// </summary>
    public required IReadOnlyList<JobExecutionAnalyticsJobRank> TopJobsByVolume { get; init; }

    /// <summary>
    /// Gets the bounded failed-completion ranking. Jobs without failures are omitted.
    /// </summary>
    public required IReadOnlyList<JobExecutionAnalyticsJobRank> TopJobsByFailures { get; init; }

    /// <summary>
    /// Gets the bounded longest terminal attempts with at most one execution per logical job.
    /// </summary>
    public required IReadOnlyList<JobExecutionAnalyticsSlowExecution> SlowestExecutions { get; init; }
}

/// <summary>
/// Describes one fixed UTC completion interval in an analytics trend.
/// </summary>
public sealed record JobExecutionAnalyticsBucket
{
    /// <summary>
    /// Gets the inclusive bucket start.
    /// </summary>
    public DateTimeOffset StartTimeUtc { get; init; }

    /// <summary>
    /// Gets the exclusive bucket end, capped by the query end.
    /// </summary>
    public DateTimeOffset EndTimeUtc { get; init; }

    /// <summary>
    /// Gets successful completions.
    /// </summary>
    public long SucceededCount { get; init; }

    /// <summary>
    /// Gets failed completions.
    /// </summary>
    public long FailedCount { get; init; }

    /// <summary>
    /// Gets skipped completions.
    /// </summary>
    public long SkippedCount { get; init; }

    /// <summary>
    /// Gets cancelled completions.
    /// </summary>
    public long CancelledCount { get; init; }

    /// <summary>
    /// Gets the number of terminal executions that started an attempt.
    /// </summary>
    public long ExecutedTerminalCount { get; init; }

    /// <summary>
    /// Gets all terminal dispositions in the bucket.
    /// </summary>
    public long CompletedTerminalCount => SucceededCount + FailedCount + SkippedCount + CancelledCount;
}

/// <summary>
/// Describes duration distribution for terminal attempts using <c>CompletedAtUtc - StartedAtUtc</c>.
/// </summary>
public sealed record JobExecutionDurationStatistics
{
    /// <summary>
    /// Gets the number of terminal attempts in the distribution.
    /// </summary>
    public long Count { get; init; }

    /// <summary>
    /// Gets the shortest duration, or <see langword="null"/> when no terminal attempt completed.
    /// </summary>
    public TimeSpan? Minimum { get; init; }

    /// <summary>
    /// Gets the arithmetic mean duration, or <see langword="null"/> when the distribution is empty.
    /// </summary>
    public TimeSpan? Average { get; init; }

    /// <summary>
    /// Gets the nearest-rank 50th percentile duration.
    /// </summary>
    public TimeSpan? P50 { get; init; }

    /// <summary>
    /// Gets the nearest-rank 90th percentile duration.
    /// </summary>
    public TimeSpan? P90 { get; init; }

    /// <summary>
    /// Gets the nearest-rank 95th percentile duration.
    /// </summary>
    public TimeSpan? P95 { get; init; }

    /// <summary>
    /// Gets the nearest-rank 99th percentile duration.
    /// </summary>
    public TimeSpan? P99 { get; init; }

    /// <summary>
    /// Gets the longest duration, or <see langword="null"/> when no terminal attempt completed.
    /// </summary>
    public TimeSpan? Maximum { get; init; }
}

/// <summary>
/// Describes one job's terminal dispositions within an analytics range.
/// </summary>
public sealed record JobExecutionAnalyticsJobRank
{
    /// <summary>
    /// Gets the immutable user-facing job name captured by the most recently completed execution in this range.
    /// </summary>
    /// <remarks>
    /// Ranking identity remains <see cref="JobKey"/> so a display-name change does not split one logical job into
    /// multiple rows.
    /// </remarks>
    public required string JobName { get; init; }

    /// <summary>
    /// Gets the logical job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets successful completions.
    /// </summary>
    public long SucceededCount { get; init; }

    /// <summary>
    /// Gets failed completions.
    /// </summary>
    public long FailedCount { get; init; }

    /// <summary>
    /// Gets skipped completions.
    /// </summary>
    public long SkippedCount { get; init; }

    /// <summary>
    /// Gets cancelled completions.
    /// </summary>
    public long CancelledCount { get; init; }

    /// <summary>
    /// Gets all terminal dispositions.
    /// </summary>
    public long CompletedTerminalCount => SucceededCount + FailedCount + SkippedCount + CancelledCount;

    /// <summary>
    /// Gets successful completions divided by successful plus failed completions.
    /// </summary>
    public double Reliability => JobExecutionAnalyticsMath.Ratio(SucceededCount, SucceededCount + FailedCount);

}

/// <summary>
/// Describes one logical job's longest terminal attempt completed in an analytics range.
/// </summary>
public sealed record JobExecutionAnalyticsSlowExecution
{
    /// <summary>
    /// Gets the execution identifier.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the immutable user-facing job name captured by this execution.
    /// </summary>
    public required string JobName { get; init; }

    /// <summary>
    /// Gets the logical job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the terminal state.
    /// </summary>
    public JobExecutionState State { get; init; }

    /// <summary>
    /// Gets when the measured attempt started.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets when the measured attempt completed.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>
    /// Gets the measured terminal-attempt duration.
    /// </summary>
    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
}

internal readonly record struct JobExecutionAnalyticsRange(
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    TimeSpan BucketDuration,
    int BucketCount);

internal static class JobExecutionAnalyticsMath
{
    internal static double Ratio(long numerator, long denominator) =>
        denominator == 0 ? 0D : numerator / (double)denominator;
}
