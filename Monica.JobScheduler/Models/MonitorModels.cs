namespace Monica.JobScheduler.Models;

/// <summary>
/// Lightweight projection for a currently active job instance.
/// </summary>
public class LiveExecution
{
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public JobState State { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public TimeSpan? ElapsedTime { get; init; }
    public string? WorkerClientId { get; init; }
    public TimeSpan MaxExecutionTimeout { get; init; }

    /// <summary>
    /// Percentage of the configured timeout already consumed by the running instance.
    /// </summary>
    public double TimeoutProgress => ElapsedTime.HasValue && MaxExecutionTimeout.TotalSeconds > 0
        ? Math.Min(100, (ElapsedTime.Value.TotalSeconds / MaxExecutionTimeout.TotalSeconds) * 100)
        : 0;
}

/// <summary>
/// Queue counts for the non-terminal job states.
/// </summary>
public class QueueStatus
{
    public int EnqueuedCount { get; init; }
    public int ScheduledCount { get; init; }
    public int ProcessingCount { get; init; }
    public int PendingCount => EnqueuedCount + ScheduledCount;
    public int ActiveCount => EnqueuedCount + ScheduledCount + ProcessingCount;
}

/// <summary>
/// Aggregated concurrency usage for one job definition.
/// </summary>
public class ConcurrencyUsage
{
    public required string JobKey { get; init; }
    public required string JobName { get; init; }
    public int CurrentRunning { get; init; }
    public int PendingCount { get; init; }
    public int CurrentExecuting => CurrentRunning + PendingCount;
    public int MaxConcurrency { get; init; }
    public double UtilizationPercent => MaxConcurrency > 0
        ? Math.Min(100, ((double)CurrentExecuting / MaxConcurrency) * 100)
        : 0;
    public bool IsAtCapacity => CurrentExecuting >= MaxConcurrency;
}

/// <summary>
/// Combined state for the monitor overview page.
/// </summary>
public class MonitorState
{
    public List<LiveExecution> LiveExecutions { get; init; } = [];
    public QueueStatus QueueStatus { get; init; } = new();
    public List<ConcurrencyUsage> ConcurrencyUsages { get; init; } = [];
    public DateTime LastRefreshTime { get; init; }
}

/// <summary>
/// Detailed concurrency data enriched with the user-facing job name.
/// </summary>
public class ConcurrencyStatusWithName
{
    public required string JobName { get; init; }
    public required JobExecutionStatistic Statistic { get; init; }
    public double UtilizationPercent => Statistic.MaxConcurrency > 0
        ? Math.Min(100, (double)Statistic.CurrentExecutingCount / Statistic.MaxConcurrency * 100)
        : 0;
    public bool IsAtCapacity => Statistic.CurrentExecutingCount >= Statistic.MaxConcurrency;
}

/// <summary>
/// Detailed monitor state for the concurrency panel.
/// </summary>
public class ConcurrencyMonitorState
{
    public List<ConcurrencyStatusWithName> Details { get; init; } = [];
    public required ConsistencyCheckResult Consistency { get; init; }
    public DateTime LastRefreshTime { get; init; }
}
