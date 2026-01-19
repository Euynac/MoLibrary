namespace MoLibrary.JobScheduler.Abstractions;

/// <summary>
/// Result of execution slot reservation attempt.
/// </summary>
public sealed class ReservationResult
{
    public bool Reserved { get; private init; }
    public string? Reason { get; private init; }

    public static ReservationResult Success() => new() { Reserved = true };
    public static ReservationResult Failure(string reason) => new() { Reserved = false, Reason = reason };
}

#region Consistency Check Types

/// <summary>
/// Result of consistency check between in-memory state and database state.
/// </summary>
public class ConsistencyCheckResult
{
    /// <summary>
    /// Whether in-memory state is consistent with database state.
    /// </summary>
    public bool IsConsistent => TotalDeviation == 0;

    /// <summary>
    /// Total deviation count (sum of absolute deviations across all jobs).
    /// </summary>
    public int TotalDeviation { get; init; }

    /// <summary>
    /// Per-job deviation details.
    /// </summary>
    public required IReadOnlyList<JobConsistencyDeviation> Deviations { get; init; }

    /// <summary>
    /// Timestamp when the check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Consistency deviation details for a single job.
/// </summary>
public class JobConsistencyDeviation
{
    /// <summary>
    /// The job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// In-memory running count.
    /// </summary>
    public int MemoryRunningCount { get; init; }

    /// <summary>
    /// Database Processing state count.
    /// </summary>
    public int DatabaseProcessingCount { get; init; }

    /// <summary>
    /// In-memory pending reservation count.
    /// </summary>
    public int MemoryPendingCount { get; init; }

    /// <summary>
    /// Database Enqueued state count.
    /// </summary>
    public int DatabaseEnqueuedCount { get; init; }

    /// <summary>
    /// Running state deviation (Memory - Database).
    /// Positive means memory has more, negative means database has more.
    /// </summary>
    public int RunningDeviation => MemoryRunningCount - DatabaseProcessingCount;

    /// <summary>
    /// Pending state deviation (Memory - Database).
    /// </summary>
    public int PendingDeviation => MemoryPendingCount - DatabaseEnqueuedCount;

    /// <summary>
    /// Whether this job has any deviation.
    /// </summary>
    public bool HasDeviation => RunningDeviation != 0 || PendingDeviation != 0;
}

/// <summary>
/// Result of reconciliation operation.
/// </summary>
public class ReconcileResult
{
    /// <summary>
    /// Whether the reconciliation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if reconciliation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// State before reconciliation.
    /// </summary>
    public ConsistencyCheckResult? StateBefore { get; init; }

    /// <summary>
    /// State after reconciliation.
    /// </summary>
    public ConsistencyCheckResult? StateAfter { get; init; }

    /// <summary>
    /// Timestamp when reconciliation was performed.
    /// </summary>
    public DateTime ReconciledAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of the reconciliation operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    public static ReconcileResult Ok(ConsistencyCheckResult before, ConsistencyCheckResult after, TimeSpan duration)
        => new() { Success = true, StateBefore = before, StateAfter = after, Duration = duration };

    public static ReconcileResult Fail(string error)
        => new() { Success = false, ErrorMessage = error };
}

#endregion

/// <summary>
/// Manages job concurrency limits and tracks running job instances
/// </summary>
public interface IJobConcurrencyGuard
{
    /// <summary>
    /// Checks if a job can be executed without exceeding its MaxConcurrency limit
    /// </summary>
    /// <param name="jobKey">The job definition key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the job can be executed, false if it exceeds the concurrency limit</returns>
    Task<bool> CanExecuteJobAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks concurrency and reserves a slot using local lock.
    /// This method MUST be called within the control plane's dispatch logic.
    /// </summary>
    /// <param name="jobKey">The job definition key</param>
    /// <param name="instanceId">The job instance ID to reserve for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ReservationResult indicating success or failure with specific reason</returns>
    Task<ReservationResult> TryReserveExecutionSlotAsync(
        string jobKey,
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a reserved slot when job execution is cancelled before starting.
    /// This is called when event publishing fails after reservation.
    /// </summary>
    /// <param name="jobKey">The job definition key</param>
    /// <param name="instanceId">The job instance ID to release</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReleaseReservedSlotAsync(
        string jobKey,
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current number of executing instances for a specific job
    /// </summary>
    /// <param name="jobKey">The job definition key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current executing count</returns>
    Task<int> GetCurrentExecutingCountAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all execution statistics for all tracked jobs.
    /// Used by monitoring dashboards to display real-time concurrency utilization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of job key to execution statistic</returns>
    Task<IReadOnlyDictionary<string, JobExecutionStatisticSnapshot>> GetAllExecutionStatisticsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed execution statistics for all tracked jobs, including instance lists.
    /// Used by monitoring dashboards to display comprehensive concurrency information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of job key to execution statistic (includes RunningInstances and PendingReservations)</returns>
    Task<IReadOnlyDictionary<string, ControlPlane.JobExecutionStatistic>> GetDetailedExecutionStatisticsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks consistency between in-memory concurrency state and database state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Consistency check result with deviation details</returns>
    Task<ConsistencyCheckResult> CheckConsistencyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles in-memory state with database state by re-scanning all active instances.
    /// This operation acquires locks on all jobs and should be used sparingly.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reconciliation result including before/after state</returns>
    Task<ReconcileResult> ReconcileAsync(CancellationToken cancellationToken = default);
}

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
