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
}
