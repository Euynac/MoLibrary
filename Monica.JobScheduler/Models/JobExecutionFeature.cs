namespace Monica.JobScheduler.Models;

/// <summary>
/// Identifies the scheduler-owned job instance associated with one pipeline execution.
/// </summary>
/// <param name="InstanceId">The durable job-instance identifier.</param>
/// <param name="JobType">Whether the instance is recurring or triggered.</param>
public sealed record JobExecutionFeature(string InstanceId, JobType JobType);
