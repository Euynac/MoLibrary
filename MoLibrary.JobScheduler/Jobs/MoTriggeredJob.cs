using MoLibrary.JobScheduler.Abstractions;

namespace MoLibrary.JobScheduler.Jobs;

/// <summary>
/// Abstract base class for triggered jobs that execute on-demand with typed parameters.
/// Inherit from this class to define jobs that should be triggered programmatically from code or APIs
/// with strongly-typed parameter objects.
/// The job scheduler will automatically discover and register derived classes during module initialization.
/// </summary>
/// <typeparam name="TParam">
/// The type of parameter object passed to the job during execution.
/// Must be a reference type (class) and should be JSON-serializable for persistence and event distribution.
/// Use simple POCO classes with public properties for best compatibility.
/// </typeparam>
/// <remarks>
/// <para>
/// TriggeredJob{TParam} is designed for event-driven operations such as:
/// - Processing business events (e.g., order placement, user registration)
/// - Handling asynchronous workflows (e.g., email sending, document generation)
/// - Responding to external triggers (e.g., webhook processing, queue messages)
/// - Executing delayed operations (e.g., scheduled notifications, reminder emails)
/// </para>
/// <para>
/// Use the <see cref="Attributes.JobConfigAttribute"/> to configure job behavior including:
/// concurrency limits, retry counts, and timeouts. Cron schedules are not applicable for triggered jobs.
/// </para>
/// <para>
/// <b>Dependency Injection:</b> TriggeredJob{TParam} supports primary constructor dependency injection.
/// Any services registered in the DI container can be injected through the constructor.
/// Job instances are created with a scoped lifetime for each execution.
/// </para>
/// <para>
/// <b>Parameter Serialization:</b> Job parameters are serialized to JSON for persistence and
/// distribution across workers via IMoEventBus. Ensure your parameter type is JSON-serializable
/// and does not contain complex object graphs or circular references.
/// </para>
/// <para>
/// <b>Cancellation:</b> The ExecuteAsync method receives a CancellationToken that will be signaled
/// when the job should stop (due to timeout, manual cancellation, or application shutdown).
/// Implementations should respect the cancellation token and exit gracefully when signaled.
/// </para>
/// </remarks>
public abstract class MoTriggeredJob<TParam> where TParam : class, IMoTriggeredJob<TParam>
{
    /// <summary>
    /// Executes the triggered job logic with the provided parameters.
    /// This method is called by the job scheduler when the job is triggered via code or API.
    /// </summary>
    /// <param name="parameters">
    /// The strongly-typed parameter object containing data needed for job execution.
    /// This object is deserialized from JSON after being distributed via IMoEventBus.
    /// Ensure the parameter type is JSON-serializable and contains all necessary data.
    /// </param>
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
    /// If RetryCount is configured, the job will be automatically retried with the same parameters.
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
    /// <para>
    /// <b>Parameter Validation:</b> Always validate the parameters object at the beginning of execution.
    /// The parameter object may be null if the job was triggered without parameters, or may contain
    /// invalid data if serialization/deserialization encountered issues.
    /// </para>
    /// </remarks>
    public abstract Task ExecuteAsync(TParam parameters, CancellationToken cancellationToken);
}
