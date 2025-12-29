using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Configuration options for the Job Scheduler module.
/// Provides settings for debug modes, worker thread limits, and custom metadata store registration.
/// </summary>
public class ModuleJobSchedulerOption : MoModuleOption<ModuleJobScheduler>
{
    /// <summary>
    /// Gets or sets a value indicating whether recurring job debug mode is enabled.
    /// When enabled, recurring jobs will not be automatically scheduled based on cron expressions.
    /// Jobs can still be triggered manually via the API for testing purposes.
    /// </summary>
    /// <value>
    /// <c>true</c> to disable automatic recurring job scheduling; otherwise, <c>false</c>.
    /// Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// This mode is useful during development and testing when you want to control job execution
    /// manually without cron-based automatic scheduling interfering with your tests.
    /// </remarks>
    public bool RecurringJobDebugMode { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether triggered job debug mode is enabled.
    /// When enabled, triggered jobs will be created but not automatically published to the event bus.
    /// This allows inspection of job instances without actual execution.
    /// </summary>
    /// <value>
    /// <c>true</c> to disable automatic triggered job execution; otherwise, <c>false</c>.
    /// Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// This mode is useful during development and testing when you want to create job instances
    /// (e.g., via EnqueueAsync) but delay or prevent their automatic execution. Jobs can still
    /// be triggered manually via the API.
    /// </remarks>
    public bool TriggeredJobDebugMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of concurrent job executions allowed on this worker instance.
    /// When set to <c>null</c>, there is no limit on concurrent executions (unlimited).
    /// When set to a positive integer, the worker will limit concurrent job executions to that number.
    /// </summary>
    /// <value>
    /// The maximum number of concurrent worker execution threads, or <c>null</c> for unlimited.
    /// Default is <c>null</c> (unlimited).
    /// </value>
    /// <remarks>
    /// <para>
    /// This setting controls the worker-level thread limit. It is different from per-job concurrency
    /// limits (MaxConcurrency in JobConfigAttribute), which control how many instances of a specific
    /// job can run concurrently.
    /// </para>
    /// <para>
    /// Use this setting to prevent resource exhaustion when running many different job types.
    /// For example, setting this to 10 means the worker will execute at most 10 jobs concurrently,
    /// regardless of job type.
    /// </para>
    /// <para>
    /// <b>Performance Consideration:</b> Setting this too low may cause job queuing and increased
    /// latency. Setting this too high may cause resource contention. A good starting point is
    /// 2-4 times the number of CPU cores.
    /// </para>
    /// </remarks>
    public int? MaxWorkerExecutionThreads { get; set; } = null;

    /// <summary>
    /// Gets or sets the maximum time to wait for RegisterCentre registration to complete before starting job scheduler services.
    /// This ensures that job scheduler services only start after the service has successfully registered to the register centre.
    /// </summary>
    /// <value>
    /// The timeout duration for waiting for registration completion.
    /// Default is 5 minutes.
    /// </value>
    /// <remarks>
    /// <para>
    /// This setting applies to ControlPlane services (JobSchedulerHostedService, JobConcurrencyGuardHostedService)
    /// and WorkerPlane registration service (JobRegistrationHostedService).
    /// </para>
    /// <para>
    /// If registration does not complete within this timeout, services will start anyway in degraded mode.
    /// The JobWorkerManager service is not affected by this setting and will start immediately.
    /// </para>
    /// </remarks>
    public TimeSpan RegistrationWaitTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to skip waiting for RegisterCentre registration.
    /// When enabled, job scheduler services will start immediately without waiting for registration.
    /// </summary>
    /// <value>
    /// <c>true</c> to skip waiting for registration; otherwise, <c>false</c>.
    /// Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// This mode is useful during development and testing when you want to test job scheduler
    /// functionality without setting up a register centre, or when running in standalone mode.
    /// </remarks>
    public bool SkipRegistrationWait { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether zombie job detection is enabled.
    /// When enabled, the system will periodically scan for stuck jobs in Processing or Enqueued states
    /// and automatically mark them as Failed after timeout.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable zombie detection; otherwise, <c>false</c>.
    /// Default is <c>true</c>.
    /// </value>
    public bool EnableZombieDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval for scanning zombie job instances.
    /// The zombie detector runs periodically to find jobs that have exceeded their timeout limits.
    /// </summary>
    /// <value>
    /// The interval between zombie detection scans.
    /// Default is 2 minutes.
    /// </value>
    /// <remarks>
    /// Setting this too low may cause unnecessary database queries. Setting this too high may delay
    /// zombie detection. A good balance is 1-5 minutes depending on job criticality.
    /// </remarks>
    public TimeSpan ZombieDetectionInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the timeout multiplier for Processing state jobs.
    /// Used to calculate effective timeout: MaxExecutionTimeout * ProcessingTimeoutMultiplier.
    /// This provides a grace period beyond the configured job timeout before marking as zombie.
    /// </summary>
    /// <value>
    /// The multiplier applied to MaxExecutionTimeout.
    /// Default is 2.0 (allows jobs to run up to 2x their configured timeout).
    /// </value>
    /// <remarks>
    /// <para>
    /// Example: If a job has MaxExecutionTimeout = 1 hour and ProcessingTimeoutMultiplier = 2.0,
    /// the job will be marked as zombie after 2 hours in Processing state.
    /// </para>
    /// <para>
    /// This grace period accounts for clock skew, worker reporting delays, and transient issues.
    /// </para>
    /// </remarks>
    public double ProcessingTimeoutMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the timeout for jobs stuck in Enqueued state.
    /// Jobs that remain in Enqueued state longer than this duration will be marked as Failed.
    /// </summary>
    /// <value>
    /// The timeout for Enqueued state.
    /// Default is 10 minutes.
    /// </value>
    /// <remarks>
    /// <para>
    /// This catches jobs that were queued but never picked up by any worker, indicating
    /// either worker unavailability or event bus delivery issues.
    /// </para>
    /// <para>
    /// Set this based on your worker pool size and expected queue processing time.
    /// In a healthy system, jobs should transition from Enqueued to Processing within seconds.
    /// </para>
    /// </remarks>
    public TimeSpan EnqueuedStateTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets a value indicating whether to check worker health before marking jobs as zombies.
    /// When enabled, jobs on offline workers are marked as Failed immediately without waiting for timeout.
    /// </summary>
    /// <value>
    /// <c>true</c> to check worker health; otherwise, <c>false</c>.
    /// Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Requires RegisterCentre integration. If RegisterCentre is unavailable, this setting is ignored.
    /// </remarks>
    public bool CheckWorkerHealthBeforeZombieDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable long-interval scheduler service.
    /// When enabled, recurring jobs with cron intervals exceeding the timer threshold will use Scheduled mode.
    /// Default is <c>true</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable long-interval job support; otherwise, <c>false</c>.
    /// Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// When disabled, recurring jobs with long intervals will log warnings but still attempt to use Timer,
    /// which may fail if the interval exceeds .NET Timer's maximum limit (~24.8 days).
    /// </remarks>
    public bool EnableLongIntervalScheduler { get; set; } = true;

    /// <summary>
    /// Gets or sets the scan interval for long-interval jobs.
    /// The LongIntervalSchedulerService runs periodically to check Scheduled jobs and convert them to Timer mode when appropriate.
    /// </summary>
    /// <value>
    /// The interval between scans.
    /// Default is 8 days. Recommended: 1/3 of TimerSafetyThresholdDays to provide sufficient buffer.
    /// </value>
    /// <remarks>
    /// Setting this too low increases database queries. Setting this too high may delay job execution.
    /// A good balance is 1/3 of the timer threshold (e.g., 8 days for 24-day threshold).
    /// </remarks>
    public TimeSpan LongIntervalScanInterval { get; set; } = TimeSpan.FromDays(8);

    /// <summary>
    /// Gets or sets the Timer safety threshold in days.
    /// Jobs with intervals exceeding this threshold will use Scheduled mode instead of Timer.
    /// </summary>
    /// <value>
    /// The threshold in days.
    /// Default is 24 days (safe for .NET Timer's ~24.8 day limit).
    /// </value>
    /// <remarks>
    /// <para>
    /// .NET Timer has a maximum interval of uint.MaxValue milliseconds (~4.2 billion ms ≈ 24.8 days).
    /// This threshold provides a safety margin to prevent Timer overflow.
    /// </para>
    /// <para>
    /// Setting this too low may unnecessarily use Scheduled mode for jobs that could use Timer.
    /// Setting this too high risks Timer overflow failures.
    /// </para>
    /// </remarks>
    public int TimerSafetyThresholdDays { get; set; } = 24;
}
