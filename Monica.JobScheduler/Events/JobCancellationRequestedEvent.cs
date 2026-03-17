namespace Monica.JobScheduler.Events;

/// <summary>
/// Event published when a scheduled job cancellation is requested.
/// </summary>
public class JobCancellationRequestedEvent
{
    public required string SchedulerScopeKey { get; init; }
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public required DateTime RequestedAt { get; init; }
}
