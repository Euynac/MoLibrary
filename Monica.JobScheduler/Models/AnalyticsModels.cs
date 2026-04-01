namespace Monica.JobScheduler.Models;

/// <summary>
/// Time aggregation granularity used by analytics queries.
/// </summary>
public enum TimeGranularity
{
    Hourly,
    Daily,
    Weekly
}

/// <summary>
/// Ranking mode used by analytics summaries.
/// </summary>
public enum RankingType
{
    ExecutionCount,
    FailureCount,
    AverageDuration,
    SkipCount
}

/// <summary>
/// Aggregated analytics payload used by the statistics page.
/// </summary>
public class JobAnalyticsSnapshot
{
    public List<ExecutionTrendPoint> TrendData { get; init; } = [];
    public DurationPercentiles Percentiles { get; init; } = new();
    public List<SlowestInstance> SlowestInstances { get; init; } = [];
    public List<JobRanking> TopByExecution { get; init; } = [];
    public List<JobRanking> TopByFailure { get; init; } = [];
    public FailureAnalysis? FailureAnalysis { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeGranularity Granularity { get; init; }
}

/// <summary>
/// Trend bucket for the execution chart.
/// </summary>
public class ExecutionTrendPoint
{
    public DateTime Timestamp { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public int CancelledCount { get; init; }
    public int TotalCount { get; init; }
    public double SuccessRate => TotalCount > 0 ? ((double)SucceededCount / TotalCount) * 100 : 100;
}

/// <summary>
/// Percentile distribution for completed job durations.
/// </summary>
public class DurationPercentiles
{
    public string? JobKey { get; init; }
    public TimeSpan P50 { get; init; }
    public TimeSpan P90 { get; init; }
    public TimeSpan P95 { get; init; }
    public TimeSpan P99 { get; init; }
    public TimeSpan Average { get; init; }
    public TimeSpan Min { get; init; }
    public TimeSpan Max { get; init; }
    public int SampleCount { get; init; }
}

/// <summary>
/// Ranked analytics item for one job definition.
/// </summary>
public class JobRanking
{
    public int Rank { get; init; }
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public double Value { get; init; }
    public string? FormattedValue { get; init; }
    public double Percentage { get; init; }
}

/// <summary>
/// Failure analysis summary for a time window.
/// </summary>
public class FailureAnalysis
{
    public List<JobFailureCount> ByJob { get; init; } = [];
    public double RetrySuccessRate { get; init; }
    public int TotalFailures { get; init; }
    public int TotalTerminated { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
}

/// <summary>
/// Failure totals for a single job definition.
/// </summary>
public class JobFailureCount
{
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public int FailedCount { get; init; }
    public int TerminatedCount { get; init; }
    public int TotalExecutions { get; init; }
    public int TotalFailureCount => FailedCount + TerminatedCount;
    public double FailureRate => TotalExecutions > 0 ? ((double)TotalFailureCount / TotalExecutions) * 100 : 0;
}

/// <summary>
/// Slowest execution sample for one job definition.
/// </summary>
public class SlowestInstance
{
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime CompletedAt { get; init; }
}
