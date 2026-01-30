namespace Monica.JobScheduler.Models;

/// <summary>
/// Represents the type of job definition in the job scheduler system.
/// Determines how the job is scheduled and executed.
/// </summary>
public enum JobType
{
    /// <summary>
    /// A recurring job that executes automatically based on a cron schedule.
    /// Recurring jobs are scheduled by the control plane according to their cron expression,
    /// start time, and end time configuration. They execute periodically without manual triggering.
    /// Examples: Daily data cleanup, hourly report generation, periodic health checks.
    /// </summary>
    Recurring,

    /// <summary>
    /// A triggered job that executes on-demand with optional parameters.
    /// Triggered jobs are explicitly invoked through code or API, optionally with a delay.
    /// They can accept typed parameters that are serialized and passed to the job execution method.
    /// Examples: User-initiated operations, event-driven processing, manual report generation.
    /// </summary>
    Triggered
}
