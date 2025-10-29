namespace MoLibrary.TaskScheduler.Events;

/// <summary>
/// Event model for publishing task execution requests via IMoEventBus.
/// This event is published when a task needs to be executed, either from
/// recurring task scheduling or manual triggered task invocation.
/// </summary>
public class TaskExecutionEvent
{
    /// <summary>
    /// Gets the unique identifier for this task instance.
    /// This ID is used to track the execution lifecycle and state of this specific task run.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the task key that identifies which task definition should be executed.
    /// This corresponds to TaskDefinition.TaskKey in the metadata store.
    /// </summary>
    public required string TaskKey { get; init; }

    /// <summary>
    /// Gets the JSON-serialized parameters for triggered tasks.
    /// For recurring tasks, this is null. For triggered tasks (TriggeredTask&lt;TParam&gt;),
    /// this contains the serialized TParam object that will be passed to ExecuteAsync.
    /// </summary>
    public string? Parameters { get; init; }

    /// <summary>
    /// Gets the timestamp when this task execution was requested.
    /// This is used for tracking and auditing purposes.
    /// </summary>
    public required DateTime RequestedAt { get; init; }
}
