namespace MoLibrary.JobScheduler.Events;

/// <summary>
/// Event model for publishing job execution requests via IMoEventBus.
/// This event is published when a job needs to be executed, either from
/// recurring job scheduling or manual triggered job invocation.
/// </summary>
public class MoJobExecutionEvent
{
    /// <summary>
    /// Gets the unique identifier for this job instance.
    /// This ID is used to track the execution lifecycle and state of this specific job run.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the job key that identifies which job definition should be executed.
    /// This corresponds to JobDefinition.JobKey in the metadata store.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the JSON-serialized parameters for triggered jobs.
    /// For recurring jobs, this is null. For triggered jobs (TriggeredJob&lt;TParam&gt;),
    /// this contains the serialized TParam object that will be passed to ExecuteAsync.
    /// </summary>
    public string? Parameters { get; init; }

    /// <summary>
    /// Gets the timestamp when this job execution was requested.
    /// This is used for tracking and auditing purposes.
    /// </summary>
    public required DateTime RequestedAt { get; init; }
}
