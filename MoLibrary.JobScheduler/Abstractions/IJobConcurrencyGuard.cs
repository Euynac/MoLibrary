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
}
