namespace MoLibrary.TaskScheduler.Models;

/// <summary>
/// Represents the type of task definition in the task scheduler system.
/// Determines how the task is scheduled and executed.
/// </summary>
public enum TaskType
{
    /// <summary>
    /// A recurring task that executes automatically based on a cron schedule.
    /// Recurring tasks are scheduled by the control plane according to their cron expression,
    /// start time, and end time configuration. They execute periodically without manual triggering.
    /// Examples: Daily data cleanup, hourly report generation, periodic health checks.
    /// </summary>
    Recurring,

    /// <summary>
    /// A triggered task that executes on-demand with optional parameters.
    /// Triggered tasks are explicitly invoked through code or API, optionally with a delay.
    /// They can accept typed parameters that are serialized and passed to the task execution method.
    /// Examples: User-initiated operations, event-driven processing, manual report generation.
    /// </summary>
    Triggered
}
