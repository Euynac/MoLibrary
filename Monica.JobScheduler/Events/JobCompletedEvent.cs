using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Events;

/// <summary>
/// Event published when a job instance completes execution (succeeded, failed, terminated, or cancelled)
/// </summary>
public class JobCompletedEvent
{
    public required string SchedulerScopeKey { get; init; }
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public required string WorkerClientId { get; init; }
    public required JobState FinalState { get; init; }
    public required DateTime CompletedAt { get; init; }
}
