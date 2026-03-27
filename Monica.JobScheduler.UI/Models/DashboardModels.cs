namespace Monica.JobScheduler.UI.Models;

using Monica.JobScheduler.Models;

/// <summary>
/// System health status
/// </summary>
public enum SystemHealthStatus
{
    /// <summary>
    /// Healthy - all components are functioning properly
    /// </summary>
    Healthy,

    /// <summary>
    /// Degraded - some components are abnormal but the system is still operational
    /// </summary>
    Degraded,

    /// <summary>
    /// Exception - Critical component failure
    /// </summary>
    Unhealthy
}

/// <summary>
/// Dashboard overview data
/// </summary>
public class DashboardSummary
{
    /// <summary>
    /// Total number of task definitions
    /// </summary>
    public int TotalJobs { get; set; }

    /// <summary>
    /// Number of periodic tasks
    /// </summary>
    public int RecurringJobCount { get; set; }

    /// <summary>
    /// Number of triggered tasks
    /// </summary>
    public int TriggeredJobCount { get; set; }

    /// <summary>
    /// Number of disabled tasks
    /// </summary>
    public int DisabledJobCount { get; set; }

    /// <summary>
    /// Number of instances currently running
    /// </summary>
    public int RunningNow { get; set; }

    /// <summary>
    /// Success rate (based on specified time window)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Throughput per hour
    /// </summary>
    public int ThroughputPerHour { get; set; }

    /// <summary>
    /// Distribution of instances of each state
    /// </summary>
    public Dictionary<JobState, int> StateDistribution { get; set; } = new();

    /// <summary>
    /// System health status
    /// </summary>
    public SystemHealthStatus HealthStatus { get; set; }

    /// <summary>
    /// Health status description
    /// </summary>
    public string? HealthMessage { get; set; }

    /// <summary>
    /// Statistics time window start time
    /// </summary>
    public DateTime MetricsStartTime { get; set; }

    /// <summary>
    /// Statistics time window end time
    /// </summary>
    public DateTime MetricsEndTime { get; set; }
}

/// <summary>
/// Recent activity history
/// </summary>
public class RecentActivity
{
    /// <summary>
    /// Instance ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// TaskKey
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// Task name
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// final state
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// Activity time (completion time or creation time)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Execution time (only for completed tasks)
    /// </summary>
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Problem task set
/// </summary>
public class ProblemJobs
{
    /// <summary>
    /// Continuous failed tasks
    /// </summary>
    public List<ConsecutiveFailureJob> ConsecutiveFailures { get; set; } = [];

    /// <summary>
    /// long running tasks
    /// </summary>
    public List<LongRunningJob> LongRunning { get; set; } = [];

    /// <summary>
    /// Tasks with high skip rate
    /// </summary>
    public List<HighSkipRateJob> HighSkipRate { get; set; } = [];

    /// <summary>
    /// Are there any question tasks
    /// </summary>
    public bool HasProblems => ConsecutiveFailures.Count > 0 || LongRunning.Count > 0 || HighSkipRate.Count > 0;

    /// <summary>
    /// Total number of problem tasks
    /// </summary>
    public int TotalProblemCount => ConsecutiveFailures.Count + LongRunning.Count + HighSkipRate.Count;
}

/// <summary>
/// Continuous failed tasks
/// </summary>
public class ConsecutiveFailureJob
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
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailureCount { get; set; }

    /// <summary>
    /// last failure time
    /// </summary>
    public DateTime LastFailureTime { get; set; }

    /// <summary>
    /// Instance ID of the last failed instance
    /// </summary>
    public string? LastFailureInstanceId { get; set; }
}

/// <summary>
/// long running tasks
/// </summary>
public class LongRunningJob
{
    /// <summary>
    /// Instance ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// TaskKey
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// Task name
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// elapsed time
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Maximum execution timeout
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; set; }

    /// <summary>
    /// start time
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Timeout Percent (Run/Max Timeout * 100)
    /// </summary>
    public double TimeoutPercentage => MaxExecutionTimeout.TotalSeconds > 0
        ? (ElapsedTime.TotalSeconds / MaxExecutionTimeout.TotalSeconds) * 100
        : 0;
}

/// <summary>
/// Tasks with high skip rate
/// </summary>
public class HighSkipRateJob
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
    /// Skips
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Total execution times
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Skip rate (0-100)
    /// </summary>
    public double SkipRate => TotalCount > 0 ? ((double)SkippedCount / TotalCount) * 100 : 0;

    /// <summary>
    /// Maximum number of concurrency configuration
    /// </summary>
    public int MaxConcurrency { get; set; }
}

/// <summary>
/// Pre-loaded data context for dashboard operations.
/// All data is loaded once and passed to service methods for in-memory processing.
/// </summary>
public class DashboardDataContext
{
    /// <summary>
    /// All job definitions (from cache)
    /// </summary>
    public required IReadOnlyList<JobDefinition> Definitions { get; init; }

    /// <summary>
    /// Job name lookup map
    /// </summary>
    public required IReadOnlyDictionary<string, string> JobNameMap { get; init; }

    /// <summary>
    /// Job config lookup map
    /// </summary>
    public required IReadOnlyDictionary<string, JobDefinition> JobConfigMap { get; init; }

    /// <summary>
    /// State distribution statistics (from optimized GROUP BY)
    /// </summary>
    public required IReadOnlyDictionary<JobState, int> StateDistribution { get; init; }

    /// <summary>
    /// All instances in metrics window (lightweight projection)
    /// </summary>
    public required IReadOnlyList<InstanceProjection> AllInstances { get; init; }

    /// <summary>
    /// Metrics time window start
    /// </summary>
    public required DateTime MetricsStartTime { get; init; }

    /// <summary>
    /// Metrics time window end
    /// </summary>
    public required DateTime MetricsEndTime { get; init; }
}

/// <summary>
/// Lightweight projection for instance data - only fields needed for analysis
/// </summary>
public record InstanceProjection(
    string InstanceId,
    string JobKey,
    JobState State,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);
