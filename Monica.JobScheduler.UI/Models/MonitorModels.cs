namespace Monica.JobScheduler.UI.Models;

using Abstractions;
using Monica.JobScheduler.Models;

/// <summary>
/// real-time execution information
/// </summary>
public class LiveExecution
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
    /// Current status
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// creation time
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Start execution time (Processing state only)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Elapsed time (Processing status only)
    /// </summary>
    public TimeSpan? ElapsedTime { get; set; }

    /// <summary>
    /// Worker client ID that performs the task
    /// </summary>
    public string? WorkerClientId { get; set; }

    /// <summary>
    /// Maximum execution timeout
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; set; }

    /// <summary>
    /// Timeout progress percentage (0-100)
    /// </summary>
    public double TimeoutProgress => ElapsedTime.HasValue && MaxExecutionTimeout.TotalSeconds > 0
        ? Math.Min(100, (ElapsedTime.Value.TotalSeconds / MaxExecutionTimeout.TotalSeconds) * 100)
        : 0;
}

/// <summary>
/// queue status
/// </summary>
public class QueueStatus
{
    /// <summary>
    /// Number of instances queued for execution
    /// </summary>
    public int EnqueuedCount { get; set; }

    /// <summary>
    /// Number of instances scheduled waiting to be triggered
    /// </summary>
    public int ScheduledCount { get; set; }

    /// <summary>
    /// Number of instances being processed
    /// </summary>
    public int ProcessingCount { get; set; }

    /// <summary>
    /// Total number of items waiting (Enqueued + Scheduled)
    /// </summary>
    public int PendingCount => EnqueuedCount + ScheduledCount;

    /// <summary>
    /// Total number of active instances (all non-final states)
    /// </summary>
    public int ActiveCount => EnqueuedCount + ScheduledCount + ProcessingCount;
}

/// <summary>
/// Concurrent usage
/// </summary>
public class ConcurrencyUsage
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
    /// Number of instances currently running
    /// </summary>
    public int CurrentRunning { get; set; }

    /// <summary>
    /// Number of instances waiting (slots reserved)
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Total number currently occupied (Running + Pending)
    /// </summary>
    public int CurrentExecuting => CurrentRunning + PendingCount;

    /// <summary>
    /// Maximum number of concurrencies
    /// </summary>
    public int MaxConcurrency { get; set; }

    /// <summary>
    /// Utilization percentage (0-100)
    /// </summary>
    public double UtilizationPercent => MaxConcurrency > 0
        ? Math.Min(100, ((double)CurrentExecuting / MaxConcurrency) * 100)
        : 0;

    /// <summary>
    /// Whether the maximum concurrency is reached
    /// </summary>
    public bool IsAtCapacity => CurrentExecuting >= MaxConcurrency;
}

/// <summary>
/// Monitor page refresh interval options
/// </summary>
public enum RefreshInterval
{
    /// <summary>
    /// 3 seconds
    /// </summary>
    ThreeSeconds = 3000,

    /// <summary>
    /// 5 seconds
    /// </summary>
    FiveSeconds = 5000,

    /// <summary>
    /// 10 seconds
    /// </summary>
    TenSeconds = 10000,

    /// <summary>
    /// 30 seconds
    /// </summary>
    ThirtySeconds = 30000
}

/// <summary>
/// Monitor page status
/// </summary>
public class MonitorState
{
    /// <summary>
    /// real-time execution list
    /// </summary>
    public List<LiveExecution> LiveExecutions { get; set; } = [];

    /// <summary>
    /// queue status
    /// </summary>
    public QueueStatus QueueStatus { get; set; } = new();

    /// <summary>
    /// Concurrent usage list (shows only tasks with active execution)
    /// </summary>
    public List<ConcurrencyUsage> ConcurrencyUsages { get; set; } = [];

    /// <summary>
    /// Last refresh time
    /// </summary>
    public DateTime LastRefreshTime { get; set; }
}

#region Concurrency Monitor Models

/// <summary>
/// Concurrency status with JobName (for UI display)
/// </summary>
public class ConcurrencyStatusWithName
{
    /// <summary>
    /// Task name
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// raw statistics
    /// </summary>
    public required JobExecutionStatistic Statistic { get; set; }

    /// <summary>
    /// Utilization percentage
    /// </summary>
    public double UtilizationPercent => Statistic.MaxConcurrency > 0
        ? Math.Min(100, (double)Statistic.CurrentExecutingCount / Statistic.MaxConcurrency * 100)
        : 0;

    /// <summary>
    /// Whether the maximum concurrency is reached
    /// </summary>
    public bool IsAtCapacity => Statistic.CurrentExecutingCount >= Statistic.MaxConcurrency;
}

/// <summary>
/// Concurrency monitoring status (including detailed information and consistency checks)
/// </summary>
public class ConcurrencyMonitorState
{
    /// <summary>
    /// Detailed list of concurrent states (with names)
    /// </summary>
    public List<ConcurrencyStatusWithName> Details { get; set; } = [];

    /// <summary>
    /// Consistency test results
    /// </summary>
    public required ConsistencyCheckResult Consistency { get; set; }

    /// <summary>
    /// Last refresh time
    /// </summary>
    public DateTime LastRefreshTime { get; set; }
}

#endregion
