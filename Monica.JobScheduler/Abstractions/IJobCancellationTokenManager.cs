namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Manages distributed cancellation tokens for job instances in the JobScheduler module.
/// Provides automatic key prefixing to isolate job cancellation tokens in a dedicated namespace.
/// </summary>
public interface IJobCancellationTokenManager
{
    /// <summary>
    /// Gets or creates a distributed cancellation token for the specified job instance.
    /// </summary>
    /// <param name="jobInstanceId">The unique identifier of the job instance.</param>
    /// <param name="cancellationToken">Optional cancellation token for the operation itself.</param>
    /// <returns>A cancellation token that can be used to cancel the job execution across distributed workers.</returns>
    Task<CancellationToken> GetOrCreateJobTokenAsync(string jobInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the distributed cancellation token for the specified job instance.
    /// This signals all workers monitoring the token to cancel the job execution.
    /// </summary>
    /// <param name="jobInstanceId">The unique identifier of the job instance.</param>
    /// <param name="cancellationToken">Optional cancellation token for the operation itself.</param>
    Task CancelJobTokenAsync(string jobInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the distributed cancellation token for the specified job instance.
    /// This should be called after job execution completes to clean up resources.
    /// </summary>
    /// <param name="jobInstanceId">The unique identifier of the job instance.</param>
    /// <param name="cancellationToken">Optional cancellation token for the operation itself.</param>
    Task DeleteJobTokenAsync(string jobInstanceId, CancellationToken cancellationToken = default);
}
