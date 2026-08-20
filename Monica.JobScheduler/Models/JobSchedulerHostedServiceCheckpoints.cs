namespace Monica.JobScheduler.Models;

/// <summary>
/// Shared hosted service checkpoints used within the JobScheduler module.
/// </summary>
public static class JobSchedulerHostedServiceCheckpoints
{
    /// <summary>
    /// Indicates that the control plane is subscribed for worker publications and its local declarations have been
    /// reconciled for the current leader cycle. Remote worker snapshots continue to arrive through anti-entropy
    /// publication after this readiness boundary.
    /// </summary>
    public const string JobDefinitionsReady = nameof(JobDefinitionsReady);

    /// <summary>
    /// Indicates that the elected scheduler has loaded persisted definitions and subscribed for definition changes.
    /// </summary>
    public const string SchedulerReady = nameof(SchedulerReady);

    /// <summary>
    /// Indicates that the elected concurrency guard has loaded persisted execution state and subscribed for changes.
    /// </summary>
    public const string ConcurrencyGuardReady = nameof(ConcurrencyGuardReady);
}
