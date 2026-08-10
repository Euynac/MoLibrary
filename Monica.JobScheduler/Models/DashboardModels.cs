namespace Monica.JobScheduler.Models;

/// <summary>
/// Aggregated dashboard payload used by the Job Scheduler UI.
/// </summary>
public class JobDashboardSnapshot
{
    /// <summary>
    /// Summary cards and state distribution data.
    /// </summary>
    public required DashboardSummary Summary { get; init; }

    /// <summary>
    /// Most recent job activity records.
    /// </summary>
    public required IReadOnlyList<RecentActivity> RecentActivities { get; init; }

    /// <summary>
    /// Derived problem indicators for the monitored jobs.
    /// </summary>
    public required ProblemJobs Problems { get; init; }
}

/// <summary>
/// Dashboard overview metrics for the scheduler.
/// </summary>
public class DashboardSummary
{
    public int TotalJobs { get; init; }
    public int RecurringJobCount { get; init; }
    public int TriggeredJobCount { get; init; }
    public int DisabledJobCount { get; init; }
    public int RunningNow { get; init; }
    public double SuccessRate { get; init; }
    public int ThroughputPerHour { get; init; }
    public Dictionary<JobState, int> StateDistribution { get; init; } = [];
    public DateTime MetricsStartTime { get; init; }
    public DateTime MetricsEndTime { get; init; }
}

/// <summary>
/// Represents a single activity item for the dashboard timeline.
/// </summary>
public class RecentActivity
{
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public JobState State { get; init; }
    public DateTime Timestamp { get; init; }
    public TimeSpan? Duration { get; init; }
}

/// <summary>
/// Aggregates the dashboard problem indicators.
/// </summary>
public class ProblemJobs
{
    public List<ConsecutiveFailureJob> ConsecutiveFailures { get; init; } = [];
    public List<LongRunningJob> LongRunning { get; init; } = [];
    public List<HighSkipRateJob> HighSkipRate { get; init; } = [];

    /// <summary>
    /// Indicates whether at least one problem bucket has data.
    /// </summary>
    public bool HasProblems => ConsecutiveFailures.Count > 0 || LongRunning.Count > 0 || HighSkipRate.Count > 0;

    /// <summary>
    /// Total number of problem entries across all buckets.
    /// </summary>
    public int TotalProblemCount => ConsecutiveFailures.Count + LongRunning.Count + HighSkipRate.Count;
}

/// <summary>
/// Describes a job with repeated terminal failures.
/// </summary>
public class ConsecutiveFailureJob
{
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public int ConsecutiveFailureCount { get; init; }
    public DateTime LastFailureTime { get; init; }
    public string? LastFailureInstanceId { get; init; }
}

/// <summary>
/// Describes a currently running job instance that is close to timing out.
/// </summary>
public class LongRunningJob
{
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public TimeSpan MaxExecutionTimeout { get; init; }
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Percentage of the configured timeout already consumed by the running instance.
    /// </summary>
    public double TimeoutPercentage => MaxExecutionTimeout.TotalSeconds > 0
        ? (ElapsedTime.TotalSeconds / MaxExecutionTimeout.TotalSeconds) * 100
        : 0;
}

/// <summary>
/// Describes a job that skips an unusually high share of executions.
/// </summary>
public class HighSkipRateJob
{
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public int SkippedCount { get; init; }
    public int TotalCount { get; init; }
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// Percentage of skipped executions within the sampled window.
    /// </summary>
    public double SkipRate => TotalCount > 0 ? ((double)SkippedCount / TotalCount) * 100 : 0;
}
