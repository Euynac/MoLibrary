using Microsoft.Extensions.Logging;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Provides common scheduler job capabilities shared by recurring and triggered jobs.
/// </summary>
/// <remarks>
/// <para>
/// This base class supplies a type-specific <see cref="Logger"/> and the protected
/// <see cref="RecordExecutionLogAsync"/> helper that appends curated entries to the current
/// job instance history.
/// </para>
/// <para>
/// Execution history logging is only available while the scheduler is actively invoking the
/// current job instance. Derived jobs should use it for milestone and summary messages that
/// are meaningful in the Job Instance detail view, not as a replacement for normal application logs.
/// </para>
/// </remarks>
public abstract class JobBase : IJobExecutionLogBindingTarget
{
    private JobExecutionLogWriter? _executionLogWriter;

    /// <summary>
    /// Initializes the job with a logger owned by the current host.
    /// </summary>
    /// <param name="logger">The logger for the concrete job type.</param>
    protected JobBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the type-specific logger for the current job instance.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Writes a curated execution log entry into the current job instance history.
    /// Use this for milestone and summary entries that should be visible from the Job Instance detail view.
    /// This API is only available while the scheduler is actively executing the current job instance.
    /// </summary>
    /// <param name="message">Developer-facing message to append to the job instance history.</param>
    /// <param name="logLevel">Optional severity prefix stored with the entry. Information does not add a prefix.</param>
    /// <param name="exception">Optional exception details appended after the message.</param>
    /// <param name="cancellationToken">Cancellation token for the persistence operation.</param>
    protected Task RecordExecutionLogAsync(
        string message,
        LogLevel logLevel = LogLevel.Information,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message) && exception == null)
        {
            throw new ArgumentException("Either a message or an exception must be provided.", nameof(message));
        }

        var writer = _executionLogWriter
            ?? throw new InvalidOperationException(
                "Job execution history logging is only available while the current job instance is executing.");

        return writer.WriteAsync(message, logLevel, exception, cancellationToken);
    }

    void IJobExecutionLogBindingTarget.BindExecutionLogWriter(JobExecutionLogWriter writer)
    {
        _executionLogWriter = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    void IJobExecutionLogBindingTarget.ClearExecutionLogWriter()
    {
        _executionLogWriter = null;
    }
}
