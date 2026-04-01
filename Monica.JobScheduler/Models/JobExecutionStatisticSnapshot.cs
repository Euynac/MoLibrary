namespace Monica.JobScheduler.Models;

/// <summary>
/// Snapshot of job execution statistics for monitoring purposes
/// </summary>
public class JobExecutionStatisticSnapshot
{
    /// <summary>
    /// The job definition key
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Maximum concurrent executions allowed
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// Number of currently running instances
    /// </summary>
    public int RunningCount { get; init; }

    /// <summary>
    /// Number of pending reservations (passed concurrency check but not yet started)
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Total executing count (running + pending)
    /// </summary>
    public int CurrentExecutingCount => RunningCount + PendingCount;

    /// <summary>
    /// Whether the job can accept new executions
    /// </summary>
    public bool CanExecute => CurrentExecutingCount < MaxConcurrency;
}
