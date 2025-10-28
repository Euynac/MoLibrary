namespace MoLibrary.TaskScheduler.Tasks;

/// <summary>
/// Abstract base class for recurring tasks that execute on a schedule.
/// Inherit from this class to define tasks that should run automatically based on cron expressions.
/// The task scheduler will automatically discover and register derived classes during module initialization.
/// </summary>
/// <remarks>
/// <para>
/// RecurringTask is designed for scheduled operations such as:
/// - Data cleanup operations (e.g., purging old records daily)
/// - Report generation (e.g., generating daily/weekly reports)
/// - Health checks and monitoring (e.g., checking system status every minute)
/// - Batch processing (e.g., processing queued items every hour)
/// </para>
/// <para>
/// Use the <see cref="Attributes.TaskConfigAttribute"/> to configure task behavior including:
/// cron schedule, concurrency limits, retry counts, timeouts, and start/end times.
/// </para>
/// <para>
/// <b>Dependency Injection:</b> RecurringTask supports primary constructor dependency injection.
/// Any services registered in the DI container can be injected through the constructor.
/// Task instances are created with a scoped lifetime for each execution.
/// </para>
/// <para>
/// <b>Cancellation:</b> The ExecuteAsync method receives a CancellationToken that will be signaled
/// when the task should stop (due to timeout, manual cancellation, or application shutdown).
/// Implementations should respect the cancellation token and exit gracefully when signaled.
/// </para>
/// </remarks>
/// <example>
/// <para><b>Basic recurring task with cron schedule:</b></para>
/// <code>
/// using MoLibrary.TaskScheduler.Tasks;
/// using MoLibrary.TaskScheduler.Attributes;
///
/// [TaskConfig(
///     TaskName = "Daily Cleanup Task",
///     Description = "Removes old records from the database",
///     CronSchedule = "0 0 2 * * *",  // Every day at 2 AM
///     MaxConcurrency = 1,
///     RetryCount = 3,
///     MaxExecutionTimeoutSeconds = 1800  // 30 minutes
/// )]
/// public class DailyCleanupTask : RecurringTask
/// {
///     public override async Task ExecuteAsync(CancellationToken cancellationToken)
///     {
///         // Check cancellation token periodically
///         cancellationToken.ThrowIfCancellationRequested();
///
///         // Perform cleanup logic
///         await Task.Delay(1000, cancellationToken);
///
///         // Log completion or handle errors as needed
///     }
/// }
/// </code>
///
/// <para><b>Recurring task with dependency injection:</b></para>
/// <code>
/// using Microsoft.Extensions.Logging;
/// using MoLibrary.TaskScheduler.Tasks;
/// using MoLibrary.TaskScheduler.Attributes;
///
/// [TaskConfig(
///     TaskName = "Report Generator",
///     Description = "Generates hourly sales reports",
///     CronSchedule = "0 0 * * * *",  // Every hour
///     MaxConcurrency = 2,
///     RetryCount = 2
/// )]
/// public class ReportGeneratorTask(
///     ILogger&lt;ReportGeneratorTask&gt; logger,
///     IReportService reportService) : RecurringTask
/// {
///     public override async Task ExecuteAsync(CancellationToken cancellationToken)
///     {
///         logger.LogInformation("Starting report generation");
///
///         try
///         {
///             await reportService.GenerateReportAsync(cancellationToken);
///             logger.LogInformation("Report generated successfully");
///         }
///         catch (Exception ex)
///         {
///             logger.LogError(ex, "Failed to generate report");
///             throw; // Re-throw to trigger retry mechanism
///         }
///     }
/// }
/// </code>
///
/// <para><b>Recurring task with time window restriction:</b></para>
/// <code>
/// [TaskConfig(
///     TaskName = "Business Hours Monitor",
///     Description = "Monitors system health during business hours",
///     CronSchedule = "0 */5 * * * *",  // Every 5 minutes
///     StartTime = "2025-01-01T09:00:00",  // Start at 9 AM
///     EndTime = "2025-12-31T17:00:00",    // End at 5 PM
///     MaxConcurrency = 1
/// )]
/// public class BusinessHoursMonitorTask : RecurringTask
/// {
///     public override async Task ExecuteAsync(CancellationToken cancellationToken)
///     {
///         // Monitor system health
///         await Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public abstract class RecurringTask
{
    /// <summary>
    /// Executes the recurring task logic.
    /// This method is called by the task scheduler according to the configured cron schedule.
    /// </summary>
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
    /// If RetryCount is configured, the task will be automatically retried.
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
    /// </remarks>
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
}
