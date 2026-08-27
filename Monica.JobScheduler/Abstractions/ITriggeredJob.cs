namespace Monica.JobScheduler.Abstractions;

/// <summary>
///     Defines interface of a triggered job.
/// </summary>
public interface ITriggeredJob<in TArgs> : IJobDefinition where TArgs : class
{
    /// <summary>
    /// Executes the triggered job logic with the provided parameters.
    /// This method is called by the job scheduler when the job is triggered via code or API.
    /// </summary>
    /// <param name="parameters">
    /// The strongly-typed parameter object containing data needed for job execution.
    /// This object is deserialized from the durable execution payload.
    /// Ensure the parameter type is JSON-serializable and contains all necessary data.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token that will be signaled when the job should stop.
    /// The token is cancelled when:
    /// - Execution timeout is reached (configured via MaxExecutionTimeoutSeconds in JobConfigAttribute)
    /// - Manual cancellation is requested through the JobScheduler facade or API
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
    /// If RetryCount is configured, the store durably requeues another attempt with the same parameters.
    /// </para>
    /// <para>
    /// <b>Timeout Handling:</b> If execution exceeds MaxExecutionTimeoutSeconds, the cancellation token
    /// will be signaled. Jobs that do not respect the cancellation token will continue to occupy
    /// worker capacity until they eventually exit; their lease cannot be safely completed by stale code.
    /// </para>
    /// <para>
    /// <b>Concurrency:</b> Multiple instances of this logical job may execute concurrently based on the
    /// MaxConcurrency setting. The owner-scoped JobKey gate includes attempts that are still stopping after a lease
    /// transition. Ensure your implementation is thread-safe or set MaxConcurrency to 1.
    /// </para>
    /// <para>
    /// <b>Parameter Validation:</b> Validate application-level values at the beginning of execution.
    /// Scheduler admission requires a non-null serialized payload and deserialization must produce a non-null object.
    /// </para>
    /// </remarks>
    Task ExecuteAsync(TArgs parameters, CancellationToken cancellationToken);
}
