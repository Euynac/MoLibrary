using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Annotations;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Abstract base class for triggered jobs that execute on-demand with typed parameters.
/// Inherit from this class to define jobs that should be triggered programmatically from code or APIs
/// with strongly-typed parameter objects.
/// The job scheduler will automatically discover and register derived classes during module initialization.
/// </summary>
/// <typeparam name="TArgs">
/// The type of parameter object passed to the job during execution.
/// Must be a reference type (class) and should be JSON-serializable for persistence and event distribution.
/// Use simple POCO classes with public properties for best compatibility.
/// </typeparam>
/// <remarks>
/// <para>
/// TriggeredJob{TArgs} is designed for event-driven operations such as:
/// - Processing business events (e.g., order placement, user registration)
/// - Handling asynchronous workflows (e.g., email sending, document generation)
/// - Responding to external triggers (e.g., webhook processing, queue messages)
/// - Executing delayed operations (e.g., scheduled notifications, reminder emails)
/// </para>
/// <para>
/// Use the <see cref="JobConfigAttribute"/> to configure job behavior including:
/// concurrency limits, retry counts, and timeouts. Cron schedules are not applicable for triggered jobs.
/// </para>
/// <para>
/// <b>Dependency Injection:</b> TriggeredJob{TArgs} supports primary constructor dependency injection.
/// Any services registered in the DI container can be injected through the constructor.
/// Job instances are created with a scoped lifetime for each execution.
/// </para>
/// <para>
/// <b>Parameter Serialization:</b> Job parameters are serialized to JSON for persistence and
/// distribution across workers via IEventBus. Ensure your parameter type is JSON-serializable
/// and does not contain complex object graphs or circular references.
/// </para>
/// <para>
/// <b>Cancellation:</b> The ExecuteAsync method receives a CancellationToken that will be signaled
/// when the job should stop (due to timeout, manual cancellation, or application shutdown).
/// Implementations should respect the cancellation token and exit gracefully when signaled.
/// </para>
/// </remarks>
/// <param name="logger">The logger for the concrete triggered job.</param>
public abstract class TriggeredJob<TArgs>(ILogger logger) : JobBase(logger), ITriggeredJob<TArgs> where TArgs : class
{
    public abstract Task ExecuteAsync(TArgs parameters, CancellationToken cancellationToken);
}
