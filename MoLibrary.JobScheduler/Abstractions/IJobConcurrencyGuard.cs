namespace MoLibrary.JobScheduler.Abstractions;

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
    /// Gets the current number of executing instances for a specific job
    /// </summary>
    /// <param name="jobKey">The job definition key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current executing count</returns>
    Task<int> GetCurrentExecutingCountAsync(string jobKey, CancellationToken cancellationToken = default);
}
