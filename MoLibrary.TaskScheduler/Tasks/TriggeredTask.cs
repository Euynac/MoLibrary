namespace MoLibrary.TaskScheduler.Tasks;

/// <summary>
/// Abstract base class for triggered tasks that execute on-demand with typed parameters.
/// Inherit from this class to define tasks that should be triggered programmatically from code or APIs
/// with strongly-typed parameter objects.
/// The task scheduler will automatically discover and register derived classes during module initialization.
/// </summary>
/// <typeparam name="TParam">
/// The type of parameter object passed to the task during execution.
/// Must be a reference type (class) and should be JSON-serializable for persistence and event distribution.
/// Use simple POCO classes with public properties for best compatibility.
/// </typeparam>
/// <remarks>
/// <para>
/// TriggeredTask{TParam} is designed for event-driven operations such as:
/// - Processing business events (e.g., order placement, user registration)
/// - Handling asynchronous workflows (e.g., email sending, document generation)
/// - Responding to external triggers (e.g., webhook processing, queue messages)
/// - Executing delayed operations (e.g., scheduled notifications, reminder emails)
/// </para>
/// <para>
/// Use the <see cref="Attributes.TaskConfigAttribute"/> to configure task behavior including:
/// concurrency limits, retry counts, and timeouts. Cron schedules are not applicable for triggered tasks.
/// </para>
/// <para>
/// <b>Dependency Injection:</b> TriggeredTask{TParam} supports primary constructor dependency injection.
/// Any services registered in the DI container can be injected through the constructor.
/// Task instances are created with a scoped lifetime for each execution.
/// </para>
/// <para>
/// <b>Parameter Serialization:</b> Task parameters are serialized to JSON for persistence and
/// distribution across workers via IMoEventBus. Ensure your parameter type is JSON-serializable
/// and does not contain complex object graphs or circular references.
/// </para>
/// <para>
/// <b>Cancellation:</b> The ExecuteAsync method receives a CancellationToken that will be signaled
/// when the task should stop (due to timeout, manual cancellation, or application shutdown).
/// Implementations should respect the cancellation token and exit gracefully when signaled.
/// </para>
/// </remarks>
/// <example>
/// <para><b>Triggered task with dependency injection:</b></para>
/// <code>
/// using Microsoft.Extensions.Logging;
/// using MoLibrary.TaskScheduler.Tasks;
/// using MoLibrary.TaskScheduler.Attributes;
///
/// public class OrderProcessingParameters
/// {
///     public string OrderId { get; set; }
///     public decimal Amount { get; set; }
///     public string CustomerId { get; set; }
/// }
///
/// [TaskConfig(
///     TaskName = "Order Processor",
///     Description = "Processes customer orders asynchronously",
///     MaxConcurrency = 10,
///     RetryCount = 5,
///     MaxExecutionTimeoutSeconds = 300
/// )]
/// public class OrderProcessingTask(
///     ILogger&lt;OrderProcessingTask&gt; logger,
///     IOrderService orderService,
///     IPaymentService paymentService) : TriggeredTask&lt;OrderProcessingParameters&gt;
/// {
///     public override async Task ExecuteAsync(
///         OrderProcessingParameters parameters,
///         CancellationToken cancellationToken)
///     {
///         logger.LogInformation(
///             "Processing order {OrderId} for customer {CustomerId}",
///             parameters.OrderId,
///             parameters.CustomerId);
///
///         try
///         {
///             // Process payment
///             await paymentService.ProcessPaymentAsync(
///                 parameters.OrderId,
///                 parameters.Amount,
///                 cancellationToken);
///
///             // Fulfill order
///             await orderService.FulfillOrderAsync(
///                 parameters.OrderId,
///                 cancellationToken);
///
///             logger.LogInformation(
///                 "Order {OrderId} processed successfully",
///                 parameters.OrderId);
///         }
///         catch (Exception ex)
///         {
///             logger.LogError(
///                 ex,
///                 "Failed to process order {OrderId}",
///                 parameters.OrderId);
///             throw; // Re-throw to trigger retry mechanism
///         }
///     }
/// }
/// </code>
///
/// <para><b>Triggering tasks from application code:</b></para>
/// <code>
/// // Inject TaskScheduler into your service
/// public class OrderController(ITaskScheduler taskScheduler)
/// {
///     [HttpPost("orders")]
///     public async Task&lt;IActionResult&gt; CreateOrder(CreateOrderRequest request)
///     {
///         // Create order in database
///         var orderId = await CreateOrderInDatabase(request);
///
///         // Trigger asynchronous order processing
///         var parameters = new OrderProcessingParameters
///         {
///             OrderId = orderId,
///             Amount = request.Amount,
///             CustomerId = request.CustomerId
///         };
///
///         // Immediate execution
///         var instanceId = await taskScheduler.EnqueueAsync&lt;OrderProcessingTask&gt;(parameters);
///
///         // Or delayed execution (e.g., process after 5 minutes)
///         var delayedInstanceId = await taskScheduler.EnqueueAsync&lt;OrderProcessingTask&gt;(
///             parameters,
///             delay: TimeSpan.FromMinutes(5));
///
///         return Ok(new { OrderId = orderId, TaskInstanceId = instanceId });
///     }
/// }
/// </code>
/// </example>
public abstract class TriggeredTask<TParam> where TParam : class
{
    /// <summary>
    /// Executes the triggered task logic with the provided parameters.
    /// This method is called by the task scheduler when the task is triggered via code or API.
    /// </summary>
    /// <param name="parameters">
    /// The strongly-typed parameter object containing data needed for task execution.
    /// This object is deserialized from JSON after being distributed via IMoEventBus.
    /// Ensure the parameter type is JSON-serializable and contains all necessary data.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token that will be signaled when the task should stop.
    /// The token is cancelled when:
    /// - Execution timeout is reached (configured via MaxExecutionTimeoutSeconds in TaskConfigAttribute)
    /// - Manual cancellation is requested via the Control Plane API
    /// - Application is shutting down gracefully
    /// Implementations should check this token periodically and exit gracefully when cancellation is requested.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// If the task completes successfully, it will be marked as Succeeded.
    /// If the task throws an exception, it will be marked as Failed and retry logic will apply if configured.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Error Handling:</b> Exceptions thrown from this method will be caught by the task executor.
    /// The task will be marked as Failed and the error message will be logged to the metadata store.
    /// If RetryCount is configured, the task will be automatically retried with the same parameters.
    /// </para>
    /// <para>
    /// <b>Timeout Handling:</b> If execution exceeds MaxExecutionTimeoutSeconds, the cancellation token
    /// will be signaled. Tasks that do not respect the cancellation token will continue to occupy
    /// worker threads but will be marked as Failed in the metadata store.
    /// </para>
    /// <para>
    /// <b>Concurrency:</b> Multiple instances of this task may execute concurrently based on the
    /// MaxConcurrency setting. Ensure your implementation is thread-safe or set MaxConcurrency to 1.
    /// </para>
    /// <para>
    /// <b>Parameter Validation:</b> Always validate the parameters object at the beginning of execution.
    /// The parameter object may be null if the task was triggered without parameters, or may contain
    /// invalid data if serialization/deserialization encountered issues.
    /// </para>
    /// </remarks>
    public abstract Task ExecuteAsync(TParam parameters, CancellationToken cancellationToken);
}
