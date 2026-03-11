namespace Monica.JobScheduler.Models;

/// <summary>
/// Shared hosted service checkpoints used within the JobScheduler module.
/// </summary>
public static class JobSchedulerHostedServiceCheckpoints
{
    /// <summary>
    /// Indicates that job definitions have been reconciled and registered for the current leader cycle.
    /// </summary>
    public const string JobDefinitionsReady = nameof(JobDefinitionsReady);
}
