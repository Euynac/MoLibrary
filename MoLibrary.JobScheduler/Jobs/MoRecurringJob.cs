namespace MoLibrary.JobScheduler.Jobs;

/// <summary>
/// Abstract base class for recurring jobs that execute on a schedule.
/// Inherit from this class to define jobs that should run automatically based on cron expressions.
/// The job scheduler will automatically discover and register derived classes during module initialization.
/// </summary>
/// <remarks>
/// <para>
/// RecurringJob is designed for scheduled operations such as:
/// - Data cleanup operations (e.g., purging old records daily)
/// - Report generation (e.g., generating daily/weekly reports)
/// - Health checks and monitoring (e.g., checking system status every minute)
/// - Batch processing (e.g., processing queued items every hour)
/// </para>
/// <para>
/// Use the <see cref="Attributes.JobConfigAttribute"/> to configure job behavior including:
/// cron schedule, concurrency limits, retry counts, timeouts, and start/end times.
/// </para>
/// <para>
/// <b>Dependency Injection:</b> RecurringJob supports primary constructor dependency injection.
/// Any services registered in the DI container can be injected through the constructor.
/// Job instances are created with a scoped lifetime for each execution.
/// </para>
/// <para>
/// <b>Cancellation:</b> The ExecuteAsync method receives a CancellationToken that will be signaled
/// when the job should stop (due to timeout, manual cancellation, or application shutdown).
/// Implementations should respect the cancellation token and exit gracefully when signaled.
/// </para>
/// </remarks>
public abstract class MoRecurringJob
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
    /// The job will be marked as Failed and the error message will be logged to the metadata store.
    /// If RetryCount is configured, the job will be automatically retried.
    /// </para>
    /// <para>
    /// <b>Timeout Handling:</b> If execution exceeds MaxExecutionTimeoutSeconds, the cancellation token
    /// will be signaled. Jobs that do not respect the cancellation token will continue to occupy
    /// worker threads but will be marked as Failed in the metadata store.
    /// </para>
    /// <para>
    /// <b>Concurrency:</b> Multiple instances of this job may execute concurrently based on the
    /// MaxConcurrency setting. Ensure your implementation is thread-safe or set MaxConcurrency to 1.
    /// </para>
    /// </remarks>
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
}
