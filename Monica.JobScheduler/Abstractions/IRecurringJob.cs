namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Defines interface of a recurring job.
/// </summary>
public interface IRecurringJob : IJobDefinition
{
    /// <summary>
    /// Executes the recurring job logic.
    /// This method is called by the job scheduler according to the configured cron schedule.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancellation token that will be signaled when the job should stop.
    /// The token is cancelled when:
    /// - Execution timeout is reached (configured via MaxExecutionTimeoutSeconds in JobConfigAttribute)
    /// - Manual cancellation is requested via the Control Plane API
    /// - Application is shutting down gracefully
    /// Implementations should check this token periodically and exit gracefully when cancellation is requested.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// If the job completes successfully, it will be marked as Succeeded.
    /// If the job throws an exception, it will be marked as Failed and retry logic will apply if configured.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Error Handling:</b> Exceptions thrown from this method will be caught by the job executor.
    /// The durable execution becomes failed, and the exception is recorded in its history.
    /// If RetryCount is configured, the store durably requeues another attempt.
    /// </para>
    /// <para>
    /// <b>Timeout Handling:</b> If execution exceeds MaxExecutionTimeoutSeconds, the cancellation token
    /// will be signaled. Jobs that do not respect the cancellation token will continue to occupy
    /// worker capacity until they eventually exit; their lease cannot be safely completed by stale code.
    /// </para>
    /// <para>
    /// <b>Concurrency:</b> Multiple instances of this logical job may execute concurrently based on the
    /// MaxConcurrency setting. When queued and running work already reaches the limit, a scheduled occurrence is
    /// recorded as skipped. The scope-wide JobKey boundary includes superseded owners and revisions that are still
    /// stopping after catalog cutover. Ensure your implementation is thread-safe or set MaxConcurrency to 1.
    /// </para>
    /// </remarks>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
