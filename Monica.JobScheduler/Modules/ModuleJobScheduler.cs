using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;
using Monica.EventBus.Abstractions;
using Monica.HealthCheck.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Services.Support;
using Monica.JobScheduler.Utils;
using Monica.StateStore.Cancellation.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJobSchedulerBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the JobScheduler module
        /// </summary>
        public ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> AddJobScheduler(Action<ModuleJobSchedulerOption>? action = null)
        {
            return builder.AddModule<ModuleJobScheduler, ModuleJobSchedulerOption>(action);
        }
    }
}

/// <summary>
/// Main module implementation for the Job Scheduler system.
/// Integrates all components including control plane (scheduling), worker plane (execution),
/// and metadata persistence layer.
/// </summary>
public class ModuleJobScheduler : MonicaModule<ModuleJobSchedulerOption>
{
    internal const string PROVIDER_FEATURE = "provider";
    internal const string METADATA_STORE_FEATURE = "metadata-store";
    internal const string SCOPE_FEATURE = "scope";

    private readonly List<JobDefinition> _jobDefinitions = [];

    /// <summary>
    /// Declares structural discovery for recurring and triggered jobs while preserving the business-type order.
    /// </summary>
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ModuleJobSchedulerOption> discovery)
    {
        var recurringJobs = TypeQuery.ConcreteClass.AssignableTo<IRecurringJob>();
        var triggeredJobs = TypeQuery.ConcreteClass.ImplementsOpenGeneric(typeof(ITriggeredJob<>));

        discovery.Match(
            TypeQuery.AnyOf(recurringJobs, triggeredJobs),
            (context, matches) =>
            {
                foreach (var match in matches)
                {
                    var isRecurring = match.Shape.IsAssignableTo(typeof(IRecurringJob));
                    var jobDefinition = ExtractJobDefinition(
                        match,
                        isRecurring ? JobType.Recurring : JobType.Triggered);

                    if (!isRecurring)
                    {
                        var triggeredInterface = match.OpenGenericInterfaces[0];
                        var argsType = triggeredInterface.GenericArguments[0];
                        jobDefinition.JobArgsClrType = argsType;
                        jobDefinition.JobArgsKey = argsType.FullName
                            ?? throw new InvalidOperationException(
                                $"Job type {match.Type.Name}'s argument type {argsType.Name} must have full name.");
                    }

                    _jobDefinitions.Add(jobDefinition);
                    context.Registrations.Add(
                        ServiceDescriptor.Transient(jobDefinition.JobClrType, jobDefinition.JobClrType));
                    Logger.LogDebug(
                        "Discovered {JobType}Job: {JobKey} ({TypeName})",
                        jobDefinition.JobType,
                        jobDefinition.JobKey,
                        jobDefinition.JobName);
                }
            });
    }

    /// <summary>
    /// Registers all discovered job types to DI and registers job definitions to JobRegistry.
    /// </summary>
    public override void PostConfigureServices(ModuleContext<ModuleJobSchedulerOption> context)
    {
        var services = context.Services;
        var serviceDiscovery = context.Modules.Get<ModuleServiceDiscovery, ModuleServiceDiscoveryOption>();
        var runsControlPlane = serviceDiscovery.Role is ServiceDiscoveryRole.Registry or ServiceDiscoveryRole.Standalone;
        IReadOnlyList<JobDefinition> jobDefinitions = Array.AsReadOnly(_jobDefinitions.ToArray());

        services.AddSingleton<IReadOnlyList<JobDefinition>>(jobDefinitions);
        services.AddSingleton<JobSchedulerFacade>();
        services.AddSingleton<JobSchedulerAnalyticsFacade>();
        services.AddSingleton<JobSchedulerDashboardFacade>();
        services.AddSingleton<JobSchedulerMonitorFacade>();
        services.AddSingleton<JobSchedulerQueryFacade>();
        services.AddSingleton<JobExecutor>();
        services.AddSingleton<JobRegistry>();
        services.AddSingleton<JobInstanceManager>();
        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<IJobDefinitionCacheService, JobDefinitionCacheServiceDefault>();
        services.AddSingleton<JobOrchestrator>();
        services.AddSingleton<ITriggeredJobManager, TriggeredJobManager>();
        services.AddSingleton<IJobCancellationTokenManager, JobCancellationTokenManager>();
        services.AddSingleton<JobHistoryCleanupExecutor>();

        services.AddSingleton<IJobConcurrencyGuard, JobConcurrencyGuardHostedService>();

        // Register job scheduler components
        services.AddSingleton<RecurringJobValidator>();
        services.AddSingleton<DelayedJobRecoveryService>();
        services.AddSingleton<RecurringJobScheduler>();
        services.AddSingleton<TriggeredJobScheduler>();

        Logger.LogInformation("Discovered {Count} job type(s) for registration", jobDefinitions.Count);

        // Readiness must reflect initialization even when the host discovers no jobs.
        services.AddHealthChecks()
            .AddMonicaReadinessCheck<JobSchedulerHealthCheck>(
                "monica.job-scheduler",
                tags: ["scheduler"]);

        
        if (runsControlPlane)
        {
            services.AddHostedService<JobSchedulerHostedService>();
            services.AddHostedService(provider => provider.GetRequiredService<IJobConcurrencyGuard>() as JobConcurrencyGuardHostedService
                                                  ?? throw new InvalidOperationException("JobConcurrencyGuard must be registered as IJobConcurrencyGuard"));

            // Register long-interval scheduler service (conditional)
            if (Option.EnableLongIntervalScheduler)
            {
                services.AddHostedService<LongIntervalSchedulerService>();
                Logger.LogInformation(
                    "Long-interval scheduler enabled with scan interval: {Interval}, threshold: {Threshold} days",
                    Option.LongIntervalScanInterval,
                    Option.TimerSafetyThresholdDays);
            }
            else
            {
                Logger.LogWarning("Long-interval scheduler disabled. Jobs with intervals >24 days may fail.");
            }

            // Register zombie detection service (conditional)
            if (Option.EnableZombieDetection)
            {
                services.AddHostedService<JobZombieDetectorHostedService>();
                Logger.LogInformation(
                    "Zombie detection enabled with interval: {Interval}",
                    Option.ZombieDetectionInterval);
            }
            else
            {
                Logger.LogInformation("Zombie detection disabled");
            }

            // Register history cleanup service (conditional)
            if (Option.EnableHistoryCleanup)
            {
                services.AddHostedService<JobHistoryCleanupHostedService>();
                Logger.LogInformation(
                    "History cleanup enabled with interval: {Interval}",
                    Option.HistoryCleanupInterval);
            }
            else
            {
                Logger.LogInformation("History cleanup disabled");
            }
        }
       
        services.AddHostedService<JobRegistrationHostedService>();
        services.AddHostedService<JobWorkerManagerHostedService>();
    }

    /// <summary>
    /// Extracts JobDefinition from a job type using reflection and JobConfigAttribute.
    /// </summary>
    private JobDefinition ExtractJobDefinition(BusinessTypeMatch match, JobType jobTypeEnum)
    {
        var jobType = match.Type;
        var attribute = match.Shape.GetAttribute<JobConfigAttribute>();

        // Create JobDefinition with defaults
        var definition = new JobDefinition
        {
            SchedulerScopeKey = Option.SchedulerScopeKey,
            JobKey = jobType.FullName ?? throw new InvalidOperationException($"Job type {jobType.Name} must have full name."),
            JobName = attribute?.JobName ?? jobType.Name,
            Description = attribute?.Description,
            JobType = jobTypeEnum,
            MaxConcurrency = attribute?.MaxConcurrencyBridge ?? 1,
            RetryCount = attribute?.RetryCountBridge ?? 0,
            MaxExecutionTimeout = attribute?.MaxExecutionTimeout ?? TimeSpan.FromHours(1),
            IsDisabled = attribute?.IsDisabledBridge ?? false,
            JobClrType = jobType,
            FromProject = Option.GetProjectName()
        };

        // Extract recurring job specific properties
        if (jobTypeEnum == JobType.Recurring)
        {
            definition.CronExpression = attribute?.CronSchedule;
            definition.StartTime = attribute?.StartTimeBridge;
            definition.EndTime = attribute?.EndTimeBridge;

            // Validate cron expression if provided
            if (!string.IsNullOrWhiteSpace(definition.CronExpression))
            {
                try
                {
                    CronHelper.Parse(definition.CronExpression);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Invalid cron expression '{CronExpression}' for job {JobKey}. Job will be disabled.",
                        definition.CronExpression, definition.JobKey);
                    definition.IsDisabled = true;
                }
            }
            else
            {
                Logger.LogWarning("RecurringJob {JobKey} has no cron expression defined. Job will be disabled.", definition.JobKey);
                definition.IsDisabled = true;
            }
        }
        return definition;
    }

    public override void Describe(ModuleDescriptor module)
    {
        // Depend on HostedService module for observable hosted services
        module.Require<ModuleHostedService, ModuleHostedServiceOption>();
        module.Require<ModuleExecutionPipeline, ModuleExecutionPipelineOption>();
        module.Require<ModuleHealthCheck, ModuleHealthCheckOption>();
        module.Require<ModuleServiceDiscovery, ModuleServiceDiscoveryOption>();
        module.RequireFeature(PROVIDER_FEATURE);
        module.RequireFeature(METADATA_STORE_FEATURE);
        module.RequireFeature(SCOPE_FEATURE);
    }

    /// <inheritdoc />
    public override void DeclareContracts(ModuleContractDescriptor<ModuleJobSchedulerOption> contracts)
    {
        contracts.RequireKeyedService<IEventBus>(nameof(ModuleJobScheduler));
        contracts.RequireKeyedService<ICancellationManager>(nameof(ModuleJobScheduler));
        contracts.RequireService<IJobMetadataRepository>();
    }
}

/// <summary>
/// Fluent configuration builder for the Job Scheduler module.
/// </summary>
public static class ModuleJobSchedulerRegistrationExtensions
{
    /// <summary>
    /// Configures a custom metadata repository implementation for job persistence.
    /// </summary>
    /// <typeparam name="TRepository">The metadata repository type implementing <see cref="IJobMetadataRepository"/>.</typeparam>
    /// <remarks>
    /// Custom repositories must be thread-safe and provide atomic state transitions.
    /// </remarks>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseCustomMetadataRepository<TRepository>(this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> module)
        where TRepository : class, IJobMetadataRepository
    {
        module.PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IJobMetadataRepository, TRepository>();
        });
        module.SatisfyFeature(ModuleJobScheduler.METADATA_STORE_FEATURE);
        return module;
    }

    /// <summary>
    /// Configures the module to use the in-memory metadata repository.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseInMemoryMetadataRepository(this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> module)
    {
        module.PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IJobMetadataRepository, InMemoryJobMetadataRepository>();
        });
        module.SatisfyFeature(ModuleJobScheduler.METADATA_STORE_FEATURE);
        return module;
    }

    /// <summary>
    /// Configures the scheduler scope key used to isolate shared persistence and events across environments.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseSchedulerScope(this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> module, string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        module.Configure(options =>
        {
            options.SchedulerScopeKey = scopeKey;
        });

        module.SatisfyFeature(ModuleJobScheduler.SCOPE_FEATURE);
        return module;
    }
    
    
    /// <summary>
    /// Configures distributed event-bus and cancellation providers. Service-discovery role and storage are composed
    /// independently; the finalized role determines whether this host runs scheduler control-plane services.
    /// </summary>
    /// <returns></returns>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseDistributedProvider(this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> module)
    {
        module.SatisfyFeature(ModuleJobScheduler.PROVIDER_FEATURE);
        module.Require<ModuleEventBus, ModuleEventBusOption>()
            .AddKeyedEventBus(nameof(ModuleJobScheduler), useDistributed: true);
        module.Require<ModuleCancellationManager, ModuleCancellationManagerOption>()
            .AddKeyedDistributedCancellationManager(nameof(ModuleJobScheduler));
        return module;
    }
   
    /// <summary>
    /// Configures process-local event-bus and cancellation providers. Service-discovery role and storage are composed
    /// independently; the finalized role determines whether this host runs scheduler control-plane services.
    /// </summary>
    /// <returns></returns>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseInMemoryProvider(this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> module)
    {
        module.SatisfyFeature(ModuleJobScheduler.PROVIDER_FEATURE);
        module.Require<ModuleEventBus, ModuleEventBusOption>()
            .AddKeyedEventBus(nameof(ModuleJobScheduler), useDistributed: false);
        module.Require<ModuleCancellationManager, ModuleCancellationManagerOption>()
            .AddKeyedInMemoryCancellationManager(nameof(ModuleJobScheduler));
        return module;
    }

}

/// <summary>
/// Configuration options for the Job Scheduler module.
/// </summary>
public class ModuleJobSchedulerOption : ModuleOptions<ModuleJobScheduler>
{
    /// <summary>
    /// Gets or sets the host-owned timezone used to evaluate recurring cron expressions and calculate upcoming runs.
    /// The default is <see cref="TimeZoneInfo.Local"/>. Configure this per Monica host when scheduler semantics must use
    /// a business timezone that differs from the operating system timezone.
    /// </summary>
    public TimeZoneInfo CronTimeZone { get; set; } = TimeZoneInfo.Local;

    /// <summary>
    /// The scheduler scope key used to isolate persistence and events across environments.
    /// This value must be explicitly configured through <see cref="ModuleJobSchedulerRegistrationExtensions.UseSchedulerScope"/>.
    /// </summary>
    public string SchedulerScopeKey { get; internal set; } = string.Empty;

    /// <summary>
    /// The project name used for job reconciliation and identification.
    /// When not configured, JobScheduler uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Resolves the project name used for job reconciliation and identification.
    /// </summary>
    public string GetProjectName()
    {
        return Application.ResolveProjectName(
            ProjectName,
            Assembly.GetEntryAssembly()?.GetName().Name);
    }

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
    /// Gets or sets how long the orchestrator waits for a job to honor timeout or host-cancellation signals before it
    /// finalizes scheduler state and continues observing the job in the background. The cancellation token and job
    /// scope remain alive until that late execution actually completes. The default is two seconds.
    /// </summary>
    public TimeSpan ExecutionCancellationGracePeriod { get; set; } = TimeSpan.FromSeconds(2);

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

    /// <summary>
    /// JSON serializer options used for serializing and deserializing job arguments.
    /// Default: Uses UnsafeRelaxedJsonEscaping to preserve non-ASCII characters (e.g., Chinese).
    /// </summary>
    public JsonSerializerOptions? JobArgsSerializerOptions { get; set; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
