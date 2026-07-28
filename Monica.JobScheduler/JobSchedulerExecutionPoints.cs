using Monica.Core.Execution;

namespace Monica.JobScheduler;

/// <summary>
/// Stable execution points emitted for scheduler-owned job attempts.
/// </summary>
public static class JobSchedulerExecutionPoints
{
    /// <summary>
    /// Represents one recurring-job attempt.
    /// </summary>
    public static readonly ExecutionPoint RecurringAttempt = new("jobs.recurring-attempt");

    /// <summary>
    /// Represents one triggered-job attempt.
    /// </summary>
    public static readonly ExecutionPoint TriggeredAttempt = new("jobs.triggered-attempt");
}
