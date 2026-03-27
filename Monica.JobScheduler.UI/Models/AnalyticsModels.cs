namespace Monica.JobScheduler.UI.Models;

/// <summary>
/// time granularity
/// </summary>
public enum TimeGranularity
{
    /// <summary>
    /// Aggregate by hour
    /// </summary>
    Hourly,

    /// <summary>
    /// Aggregate by day
    /// </summary>
    Daily,

    /// <summary>
    /// Aggregate by week
    /// </summary>
    Weekly
}

/// <summary>
/// Execution trend data points
/// </summary>
public class ExecutionTrendPoint
{
    /// <summary>
    /// Time point (start time of aggregation period)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Number of successful executions
    /// </summary>
    public int SucceededCount { get; set; }

    /// <summary>
    /// Number of failed executions (Failed + Terminated)
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// skip quantity
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Cancellation quantity
    /// </summary>
    public int CancelledCount { get; set; }

    /// <summary>
    /// Total number of executions
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Success rate for this time period (0-100)
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? ((double)SucceededCount / TotalCount) * 100 : 100;
}

/// <summary>
/// Execution time percentile
/// </summary>
public class DurationPercentiles
{
    /// <summary>
    /// Task Key (if targeting a specific task)
    /// </summary>
    public string? JobKey { get; set; }

    /// <summary>
    /// P50 (median)
    /// </summary>
    public TimeSpan P50 { get; set; }

    /// <summary>
    /// P90
    /// </summary>
    public TimeSpan P90 { get; set; }

    /// <summary>
    /// P95
    /// </summary>
    public TimeSpan P95 { get; set; }

    /// <summary>
    /// P99
    /// </summary>
    public TimeSpan P99 { get; set; }

    /// <summary>
    /// Average time taken
    /// </summary>
    public TimeSpan Average { get; set; }

    /// <summary>
    /// The shortest time
    /// </summary>
    public TimeSpan Min { get; set; }

    /// <summary>
    /// longest time
    /// </summary>
    public TimeSpan Max { get; set; }

    /// <summary>
    /// sample size
    /// </summary>
    public int SampleCount { get; set; }
}

/// <summary>
/// Ranking type
/// </summary>
public enum RankingType
{
    /// <summary>
    /// Ranked by execution count
    /// </summary>
    ExecutionCount,

    /// <summary>
    /// Ranked by number of failures
    /// </summary>
    FailureCount,

    /// <summary>
    /// Ranked by average time taken (from longest to shortest)
    /// </summary>
    AverageDuration,

    /// <summary>
    /// Ranked by skip count
    /// </summary>
    SkipCount
}

/// <summary>
/// Task ranking items
/// </summary>
public class JobRanking
{
    /// <summary>
    /// Ranking
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// TaskKey
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// Task name
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// Numeric value (different meanings depending on ranking type)
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Formatted numerical display
    /// </summary>
    public string? FormattedValue { get; set; }

    /// <summary>
    /// % of total
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// Failure analysis results
/// </summary>
public class FailureAnalysis
{
    /// <summary>
    /// Failure statistics grouped by tasks
    /// </summary>
    public List<JobFailureCount> ByJob { get; set; } = [];

    /// <summary>
    /// Retry success rate (0-100)
    /// Calculation formula: Number of successes after retries / Total number of retries * 100
    /// </summary>
    public double RetrySuccessRate { get; set; }

    /// <summary>
    /// Total failures
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// Total number of terminations
    /// </summary>
    public int TotalTerminated { get; set; }

    /// <summary>
    /// Start of statistical time range
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End of statistical time range
    /// </summary>
    public DateTime EndTime { get; set; }
}

/// <summary>
/// Task failure statistics
/// </summary>
public class JobFailureCount
{
    /// <summary>
    /// TaskKey
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// Task name
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// Number of failures (Failed status)
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Number of terminations (Terminated status)
    /// </summary>
    public int TerminatedCount { get; set; }

    /// <summary>
    /// Total number of failures (Failed + Terminated)
    /// </summary>
    public int TotalFailureCount => FailedCount + TerminatedCount;

    /// <summary>
    /// The total number of executions of this task
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Failure rate (0-100)
    /// </summary>
    public double FailureRate => TotalExecutions > 0 ? ((double)TotalFailureCount / TotalExecutions) * 100 : 0;
}

/// <summary>
/// Time range default
/// </summary>
public enum TimeRangePreset
{
    /// <summary>
    /// today
    /// </summary>
    Today,

    /// <summary>
    /// last 24 hours
    /// </summary>
    Last24Hours,

    /// <summary>
    /// Last 7 days
    /// </summary>
    Last7Days,

    /// <summary>
    /// Last 30 days
    /// </summary>
    Last30Days,

    /// <summary>
    /// Custom scope
    /// </summary>
    Custom
}

/// <summary>
/// Slowest instance information (for Top N display)
/// </summary>
public class SlowestInstance
{
    /// <summary>
    /// Instance ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// job key
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// Job name
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// Execution time
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// completion time
    /// </summary>
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Statistics page request parameters
/// </summary>
public class StatisticsRequest
{
    /// <summary>
    /// Time range default
    /// </summary>
    public TimeRangePreset Preset { get; set; } = TimeRangePreset.Today;

    /// <summary>
    /// Custom start time (only used when Preset is Custom)
    /// </summary>
    public DateTime? CustomStartTime { get; set; }

    /// <summary>
    /// Custom end time (only used when Preset is Custom)
    /// </summary>
    public DateTime? CustomEndTime { get; set; }

    /// <summary>
    /// time granularity
    /// </summary>
    public TimeGranularity Granularity { get; set; } = TimeGranularity.Hourly;

    /// <summary>
    /// Ranking display quantity
    /// </summary>
    public int TopN { get; set; } = 10;

    /// <summary>
    /// Get the actual start time
    /// </summary>
    public DateTime GetStartTime()
    {
        if (Preset == TimeRangePreset.Custom && CustomStartTime.HasValue)
            return CustomStartTime.Value;

        var now = DateTime.UtcNow;
        return Preset switch
        {
            TimeRangePreset.Today => now.Date,
            TimeRangePreset.Last24Hours => now.AddHours(-24),
            TimeRangePreset.Last7Days => now.AddDays(-7),
            TimeRangePreset.Last30Days => now.AddDays(-30),
            _ => now.Date
        };
    }

    /// <summary>
    /// Get the actual end time
    /// </summary>
    public DateTime GetEndTime()
    {
        if (Preset == TimeRangePreset.Custom && CustomEndTime.HasValue)
            return CustomEndTime.Value;

        return DateTime.UtcNow;
    }
}
