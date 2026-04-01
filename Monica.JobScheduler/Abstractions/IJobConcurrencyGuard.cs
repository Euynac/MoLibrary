using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Abstractions;

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
    Task<IReadOnlyDictionary<string, JobExecutionStatistic>> GetDetailedExecutionStatisticsAsync(
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
