using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Abstractions;

/// <summary>
/// Provides job execution functionality for different job types.
/// Handles the actual job invocation logic while the orchestrator manages state, timeout, and retries.
/// </summary>
public interface IMoJobExecutor
{
    /// <summary>
    /// Executes a recurring job.
    /// </summary>
    /// <param name="context">The execution context containing job type, service provider, and cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteRecurringJobAsync(JobExecutionContext context);

    /// <summary>
    /// Executes a triggered job with parameters.
    /// </summary>
    /// <param name="context">The execution context containing job type, arguments, service provider, and cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteTriggeredJobAsync(JobExecutionContext context);
}
