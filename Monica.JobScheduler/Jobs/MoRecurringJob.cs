using Monica.JobScheduler.Abstractions;

namespace Monica.JobScheduler.Jobs;

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
public abstract class MoRecurringJob : IMoRecurringJob
{
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
}
