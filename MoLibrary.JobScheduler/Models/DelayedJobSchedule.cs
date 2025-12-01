namespace MoLibrary.JobScheduler.Models;

/// <summary>
/// Internal model tracking delayed job scheduling state.
/// </summary>
internal class DelayedJobSchedule
{
    public required string InstanceId { get; init; }
    public required string JobKey { get; init; }
    public Timer? Timer { get; set; }
    public DateTime ScheduledExecutionTime { get; set; }
}
