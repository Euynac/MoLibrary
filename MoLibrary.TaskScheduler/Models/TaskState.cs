namespace MoLibrary.TaskScheduler.Models;

/// <summary>
/// Represents the execution state of a task instance in the task scheduler lifecycle.
/// State transitions follow a defined state machine ensuring proper task execution flow.
/// </summary>
public enum TaskState
{
    /// <summary>
    /// The task instance is scheduled for future execution at a specific time.
    /// This state is used for delayed task execution (e.g., triggered tasks with delay).
    /// Transitions to: Enqueued (when scheduled time arrives), Cancelled (if cancelled before execution)
    /// </summary>
    Scheduled,

    /// <summary>
    /// The task instance is queued and ready for execution by a worker.
    /// This is the initial state for immediate execution or after a scheduled task's delay elapses.
    /// Transitions to: Processing (when worker picks up task), Skipped (if concurrency limit exceeded), Cancelled (if cancelled before execution)
    /// </summary>
    Enqueued,

    /// <summary>
    /// The task instance is currently being executed by a worker.
    /// Workers update the instance to this state when they begin execution.
    /// Transitions to: Succeeded (on successful completion), Failed (on exception or timeout), Cancelled (if cancelled during execution)
    /// </summary>
    Processing,

    /// <summary>
    /// The task instance completed successfully without errors.
    /// This is a terminal state - no further transitions occur.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The task instance failed due to an exception or timeout.
    /// May transition to Processing if retries are configured and remaining.
    /// Transitions to: Processing (if retries remain), Terminated (if no retries remain or retry limit reached)
    /// </summary>
    Failed,

    /// <summary>
    /// The task instance has failed and exhausted all retry attempts.
    /// This is a terminal state indicating final failure after all retry attempts.
    /// </summary>
    Terminated,

    /// <summary>
    /// The task instance was manually cancelled by an administrator or system event.
    /// Cancellation can occur at any non-terminal state via the cancellation API.
    /// This is a terminal state - no further transitions occur.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The task instance was skipped because the maximum concurrency limit was reached.
    /// This occurs when a task instance is submitted but the task already has the maximum
    /// number of concurrent executions running.
    /// This is a terminal state - no further transitions occur.
    /// </summary>
    Skipped
}
