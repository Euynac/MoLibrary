namespace Monica.JobScheduler.Events;

/// <summary>
/// Event published when a manual job execution is requested from a non-Centre node.
/// The requester creates and persists the job instance first, then the Centre node dispatches it.
/// </summary>
public class ManualJobExecutionRequestEvent
{
    /// <summary>
    /// Gets the pre-generated instance ID for immediate return to caller.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the job key identifying which job to execute.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the pre-serialized JSON string of job arguments, or null for jobs without arguments.
    /// </summary>
    public string? JobArgsJson { get; init; }

    /// <summary>
    /// Gets the timestamp when the request was made.
    /// </summary>
    public required DateTime RequestedAt { get; init; }
}
