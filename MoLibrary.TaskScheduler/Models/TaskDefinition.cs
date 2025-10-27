namespace MoLibrary.TaskScheduler.Models;

/// <summary>
/// Represents the metadata and configuration for a task definition.
/// Task definitions are registered at application startup and define how tasks should be scheduled and executed.
/// </summary>
public class TaskDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier for this task definition.
    /// Defaults to the task type's full name (TypeFullName).
    /// Must be unique across all registered task definitions in the metadata store.
    /// </summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable name for this task.
    /// Defaults to the task type's simple name.
    /// Used for display purposes in UI and logs.
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description explaining the purpose of this task.
    /// Provides additional context about what the task does and when it should run.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the type of task (Recurring or Triggered).
    /// Determines whether the task is scheduled automatically or triggered on-demand.
    /// </summary>
    public TaskType Type { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent executions allowed for this task.
    /// Default is 1. When the limit is reached, new task instances will be skipped.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of automatic retry attempts on failure.
    /// Default is 0 (no retries). Failed tasks will retry up to this count before being terminated.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum execution timeout for this task.
    /// Default is 1 hour. Tasks exceeding this duration will be cancelled via IMoCancellationManager.
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets whether this task is disabled.
    /// Default is false. Disabled tasks will not be scheduled or executed.
    /// </summary>
    public bool IsDisabled { get; set; } = false;

    // Recurring task specific properties

    /// <summary>
    /// Gets or sets the cron expression for recurring task scheduling.
    /// Only applicable for recurring tasks. Supports second-level precision.
    /// Example: "*/10 * * * * *" runs every 10 seconds.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the earliest time this recurring task should start executing.
    /// Only applicable for recurring tasks. Null means no start restriction.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the latest time this recurring task should stop executing.
    /// Only applicable for recurring tasks. Null means no end restriction.
    /// </summary>
    public DateTime? EndTime { get; set; }

    // Metadata properties

    /// <summary>
    /// Gets or sets the CLR type of the task class for instantiation.
    /// Used to create task instances via dependency injection.
    /// Must inherit from RecurringTask or TriggeredTask{TParam}.
    /// </summary>
    public Type TaskClrType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the CLR type of the parameter for triggered tasks.
    /// Only applicable for TriggeredTask{TParam}. Null for recurring tasks.
    /// Used to deserialize JSON parameters when creating task instances.
    /// </summary>
    public Type? ParameterClrType { get; set; }
}
