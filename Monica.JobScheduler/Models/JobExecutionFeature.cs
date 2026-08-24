namespace Monica.JobScheduler.Models;

/// <summary>
/// Identifies the scheduler-owned durable execution associated with one pipeline invocation.
/// </summary>
/// <param name="InstanceId">The durable execution identifier.</param>
/// <param name="JobType">Whether the execution is recurring or triggered.</param>
public sealed record JobExecutionFeature(string InstanceId, JobType JobType);
