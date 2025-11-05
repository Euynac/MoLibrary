namespace MoLibrary.JobScheduler.Models;

/// <summary>
/// Represents the execution state of a job instance in the job scheduler lifecycle.
/// State transitions follow a defined state machine ensuring proper job execution flow.
/// </summary>
public enum JobState
{
    /// <summary>
    /// The job instance is scheduled for future execution at a specific time.
    /// This state is used for delayed job execution (e.g., triggered jobs with delay).
    /// Transitions to: Enqueued (when scheduled time arrives), Cancelled (if cancelled before execution)
    /// </summary>
    Scheduled,

    /// <summary>
    /// The job instance is queued and ready for execution by a worker.
    /// This is the initial state for immediate execution or after a scheduled job's delay elapses.
    /// Transitions to: Processing (when worker picks up job), Skipped (if concurrency limit exceeded), Cancelled (if cancelled before execution)
    /// </summary>
    Enqueued,

    /// <summary>
    /// The job instance is currently being executed by a worker.
    /// Workers update the instance to this state when they begin execution.
    /// Transitions to: Succeeded (on successful completion), Failed (on exception or timeout), Cancelled (if cancelled during execution)
    /// </summary>
    Processing,

    /// <summary>
    /// The job instance completed successfully without errors.
    /// This is a terminal state - no further transitions occur.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The job instance failed due to an exception or timeout.
    /// May transition to Processing if retries are configured and remaining.
    /// Transitions to: Processing (if retries remain), Terminated (if no retries remain or retry limit reached)
    /// </summary>
    Failed,

    /// <summary>
    /// The job instance has failed and exhausted all retry attempts.
    /// This is a terminal state indicating final failure after all retry attempts.
    /// </summary>
    Terminated,

    /// <summary>
    /// The job instance was manually cancelled by an administrator or system event.
    /// Cancellation can occur at any non-terminal state via the cancellation API.
    /// This is a terminal state - no further transitions occur.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The job instance was skipped because the maximum concurrency limit was reached.
    /// This occurs when a job instance is submitted but the job already has the maximum
    /// number of concurrent executions running.
    /// This is a terminal state - no further transitions occur.
    /// </summary>
    Skipped
}
