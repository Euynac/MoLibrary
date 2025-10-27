using MoLibrary.TaskScheduler.Attributes;

namespace MoLibrary.TaskScheduler.Models;

/// <summary>
/// Represents a single execution instance of a task.
/// Task instances track the lifecycle of an individual task execution from creation to completion.
/// </summary>
public class TaskInstance
{
    /// <summary>
    /// Gets or sets the unique identifier for this task instance.
    /// Generated as a GUID when the instance is created.
    /// Used as the cancellation token key in IMoCancellationManager for distributed cancellation.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task key referencing the parent TaskDefinition.
    /// Links this instance to its task definition for configuration and metadata.
    /// </summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current execution state of this task instance.
    /// State transitions follow the state machine defined in TaskState enum.
    /// </summary>
    public TaskState State { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized parameters for triggered tasks.
    /// Only applicable for TriggeredTask{TParam}. Null for recurring tasks.
    /// Deserialized and passed to the task's ExecuteAsync method.
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this task instance was created.
    /// Set when the instance is first created in the metadata store.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the scheduled execution time for delayed tasks.
    /// Only set for tasks with a delay (state starts as Scheduled).
    /// When this time arrives, the state transitions from Scheduled to Enqueued.
    /// </summary>
    public DateTime? ScheduledFor { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when task execution started.
    /// Set when a worker transitions the task to Processing state.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when task execution completed.
    /// Set when the task reaches a terminal state (Succeeded, Terminated, Cancelled, Skipped).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the error message if the task failed.
    /// Contains exception details, timeout messages, or other failure information.
    /// Populated when state transitions to Failed, Terminated, or Cancelled.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the current retry attempt number.
    /// Starts at 0 for the initial execution. Increments with each retry.
    /// Used to determine if the task should retry or transition to Terminated.
    /// </summary>
    public int RetryAttempt { get; set; } = 0;
}
