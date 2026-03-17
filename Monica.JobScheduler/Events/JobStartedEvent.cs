namespace Monica.JobScheduler.Events;

/// <summary>
/// Event published when a job instance starts executing on a worker
/// </summary>
public class JobStartedEvent
{
    public required string SchedulerScopeKey { get; init; }
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public required string WorkerClientId { get; init; }
    public required DateTime StartedAt { get; init; }
}
