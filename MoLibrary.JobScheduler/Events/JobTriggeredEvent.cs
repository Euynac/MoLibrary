namespace MoLibrary.JobScheduler.Events;

/// <summary>
/// Event published when IMoTriggeredJobManager.EnqueueAsync is called.
/// Notifies JobSchedulerHostedService to create and schedule a job instance.
/// </summary>
public class JobTriggeredEvent
{
    /// <summary>
    /// Gets the pre-generated instance ID for immediate return to caller.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the job key identifying which triggered job to execute.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the JSON-serialized job arguments.
    /// </summary>
    public required string JobArgs { get; init; }

    /// <summary>
    /// Gets the optional delay before execution.
    /// If null: immediate execution (state: Enqueued).
    /// If set: enters Scheduled state, executes after delay.
    /// </summary>
    public TimeSpan? Delay { get; init; }

    /// <summary>
    /// Gets the timestamp when the job was triggered.
    /// Used for calculating scheduled execution time.
    /// </summary>
    public required DateTime TriggeredAt { get; init; }
}
