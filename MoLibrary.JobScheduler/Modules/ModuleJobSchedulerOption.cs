using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Configuration options for the Job Scheduler module.
/// </summary>
public class ModuleJobSchedulerOption : MoModuleOption<ModuleJobScheduler>
{
    /// <summary>
    /// Disables automatic recurring job scheduling when enabled.
    /// Jobs can still be triggered manually via the API. Default: false.
    /// </summary>
    public bool RecurringJobDebugMode { get; set; } = false;

    /// <summary>
    /// Disables automatic triggered job execution when enabled.
    /// Jobs are created but not published to event bus. Default: false.
    /// </summary>
    public bool TriggeredJobDebugMode { get; set; } = false;

    /// <summary>
    /// Maximum concurrent job executions allowed on this worker instance.
    /// Null for unlimited. Default: null.
    /// </summary>
    public int? MaxWorkerExecutionThreads { get; set; } = null;

    /// <summary>
    /// Maximum time to wait for RegisterCentre registration before starting scheduler services.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan RegistrationWaitTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Skips waiting for RegisterCentre registration when enabled.
    /// Useful for development and standalone mode. Default: false.
    /// </summary>
    public bool SkipRegistrationWait { get; set; } = false;

    /// <summary>
    /// Enables periodic scanning for stuck jobs in Processing or Enqueued states.
    /// Automatically marks them as Failed after timeout. Default: true.
    /// </summary>
    public bool EnableZombieDetection { get; set; } = true;

    /// <summary>
    /// Interval between zombie detection scans. Default: 2 minutes.
    /// </summary>
    public TimeSpan ZombieDetectionInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Timeout multiplier for Processing state jobs.
    /// Effective timeout = MaxExecutionTimeout × this value. Default: 2.0.
    /// </summary>
    public double ProcessingTimeoutMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Timeout for jobs stuck in Enqueued state. Default: 10 minutes.
    /// </summary>
    public TimeSpan EnqueuedStateTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Checks worker health before marking jobs as zombies.
    /// Jobs on offline workers are marked as Failed immediately. Default: true.
    /// </summary>
    public bool CheckWorkerHealthBeforeZombieDetection { get; set; } = true;

    /// <summary>
    /// Enables long-interval scheduler for jobs exceeding timer threshold (~24.8 days).
    /// Default: true.
    /// </summary>
    public bool EnableLongIntervalScheduler { get; set; } = true;

    /// <summary>
    /// Scan interval for checking Scheduled jobs and converting them to Timer mode.
    /// Default: 8 days.
    /// </summary>
    public TimeSpan LongIntervalScanInterval { get; set; } = TimeSpan.FromDays(8);

    /// <summary>
    /// Timer safety threshold in days. Jobs exceeding this use Scheduled mode.
    /// Default: 24 days (.NET Timer limit ~24.8 days).
    /// </summary>
    public int TimerSafetyThresholdDays { get; set; } = 24;

    /// <summary>
    /// Enables periodic cleanup of old job execution instances based on retention policies.
    /// Default: true.
    /// </summary>
    public bool EnableHistoryCleanup { get; set; } = true;

    /// <summary>
    /// Interval between history cleanup scans. Default: 1 hour.
    /// </summary>
    public TimeSpan HistoryCleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum instances to delete per job per cleanup cycle.
    /// Prevents excessive deletions. Default: 5000 (0 for unlimited).
    /// </summary>
    public int MaxDeletionsPerJobPerCycle { get; set; } = 5000;

    /// <summary>
    /// Maximum retained history records for orphaned job instances.
    /// Orphaned instances are those without matching active job definitions. Default: 10 (0 for unlimited).
    /// </summary>
    public int MaxRetainedOrphanedInstances { get; set; } = 10;
}
