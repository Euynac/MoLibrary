using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.JobScheduler.Helpers;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Api;
using Monica.JobScheduler.Attributes;
using Monica.JobScheduler.Cache;
using Monica.JobScheduler.ControlPlane;
using Monica.JobScheduler.HealthChecks;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.WorkerPlane;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJobSchedulerBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 JobScheduler 模块
        /// </summary>
        public static ModuleJobSchedulerGuide AddJobScheduler(Action<ModuleJobSchedulerOption>? action = null)
        {
            return new ModuleJobSchedulerGuide().Register(action);
        }
    }
}

/// <summary>
/// Main module implementation for the Job Scheduler system.
/// Integrates all components including control plane (scheduling), worker plane (execution),
/// and metadata persistence layer.
/// </summary>
public class ModuleJobScheduler(ModuleJobSchedulerOption option)
    : MoModule<ModuleJobScheduler, ModuleJobSchedulerOption, ModuleJobSchedulerGuide>(option), IWantIterateBusinessTypes
{
    private readonly List<JobDefinition> _jobDefinitions = [];

    public override ModuleKey GetModuleKey() => EMoModuleKey.JobScheduler;

    /// <summary>
    /// Iterates through business types to discover and collect job types.
    /// Extracts metadata from JobConfigAttribute and creates JobDefinition objects.
    /// </summary>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false })
            {
                if (type.IsAssignableTo(typeof(IMoRecurringJob)))
                {
                    var jobDefinition = ExtractJobDefinition(type, JobType.Recurring);
                    _jobDefinitions.Add(jobDefinition);
                }
                else if (type.IsImplementInterfaceGeneric(typeof(IMoTriggeredJob<>), out var genericTypeDefinition))
                {
                    var argsType = genericTypeDefinition.GenericTypeArguments[0];
                    var jobDefinition = ExtractJobDefinition(type, JobType.Triggered);
                    jobDefinition.JobArgsClrType = argsType;
                    jobDefinition.JobArgsKey = argsType.FullName ?? throw new InvalidOperationException($"Job type {type.Name}'s argument type {argsType.Name} must have full name.");
                    _jobDefinitions.Add(jobDefinition);
                }
            }

            yield return type;
        }
    }

    /// <summary>
    /// Registers all discovered job types to DI and registers job definitions to JobRegistry.
    /// </summary>
    public override void PostConfigureServices(IServiceCollection services)
    {
        // Register all discovered job types as transient in DI
        foreach (var job in _jobDefinitions)
        {
            services.AddTransient(job.JobClrType);
            Logger.LogDebug("Discovered {JobType}Job: {JobKey} ({TypeName})", job.JobType, job.JobKey, job.JobName);
        }
        
        services.AddSingleton<JobSchedulerApiService>();
        services.AddSingleton<JobExecutor>();
        services.AddSingleton<JobRegistry>();
        services.AddSingleton<JobInstanceManager>();
        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<IJobDefinitionCacheService, JobDefinitionCacheServiceDefault>();
        services.AddSingleton<JobOrchestrator>();
        services.AddSingleton<IMoTriggeredJobManager, TriggeredJobManager>();
        services.AddSingleton<IJobCancellationTokenManager, JobCancellationTokenManager>();
        services.AddSingleton<JobHistoryCleanupExecutor>();

        services.AddSingleton<IJobConcurrencyGuard, JobConcurrencyGuardHostedService>();

        // Register job scheduler components
        services.AddSingleton<RecurringJobValidator>();
        services.AddSingleton<DelayedJobRecoveryService>();
        services.AddSingleton<RecurringJobScheduler>();
        services.AddSingleton<TriggeredJobScheduler>();

        Logger.LogInformation("Discovered {Count} job type(s) for registration", _jobDefinitions.Count);

        // Register health check for monitoring initialization status (always register regardless of job count)
        services.AddHealthChecks()
            .AddCheck<JobSchedulerHealthCheck>("JobScheduler", tags: ["ready", "scheduler"]);

        
        if (GetOptions<ModuleRegisterCentreOption>().IsCentreServer)
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
       
        services.AddHostedService<JobRegistrationHostedService>(provider => ActivatorUtilities.CreateInstance<JobRegistrationHostedService>(provider, _jobDefinitions));

        services.AddHostedService<JobWorkerManagerHostedService>(provider => ActivatorUtilities.CreateInstance<JobWorkerManagerHostedService>(provider, _jobDefinitions));
    }

    /// <summary>
    /// Extracts JobDefinition from a job type using reflection and JobConfigAttribute.
    /// </summary>
    private JobDefinition ExtractJobDefinition(Type jobType, JobType jobTypeEnum)
    {
        // Extract JobConfigAttribute if present
        var attribute = jobType.GetCustomAttributes(typeof(JobConfigAttribute), false)
            .FirstOrDefault() as JobConfigAttribute;

        // Create JobDefinition with defaults
        var definition = new JobDefinition
        {
            JobKey = jobType.FullName ?? throw new InvalidOperationException($"Job type {jobType.Name} must have full name."),
            JobName = attribute?.JobName ?? jobType.Name,
            Description = attribute?.Description,
            JobType = jobTypeEnum,
            MaxConcurrency = attribute?.MaxConcurrencyBridge ?? 1,
            RetryCount = attribute?.RetryCountBridge ?? 0,
            MaxExecutionTimeout = attribute?.MaxExecutionTimeout ?? TimeSpan.FromHours(1),
            IsDisabled = attribute?.IsDisabledBridge ?? false,
            JobClrType = jobType,
            FromProject = Option.ProjectName
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

    public override void ClaimDependencies()
    {
        // Depend on HostedService module for observable hosted services
        DependsOnModule<ModuleHostedServiceGuide>().Register();
    }
}

/// <summary>
/// Fluent configuration builder for the Job Scheduler module.
/// </summary>
public class ModuleJobSchedulerGuide
    : MoModuleGuide<ModuleJobScheduler, ModuleJobSchedulerOption, ModuleJobSchedulerGuide>
{
    private const string CONFIG_METADATA_STORE = nameof(CONFIG_METADATA_STORE);
    private const string CONFIG_PROVIDER = nameof(CONFIG_PROVIDER);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CONFIG_PROVIDER,CONFIG_METADATA_STORE];
    }

    /// <summary>
    /// Configures a custom metadata repository implementation for job persistence.
    /// </summary>
    /// <typeparam name="TRepository">The metadata repository type implementing <see cref="IMoJobMetadataRepository"/>.</typeparam>
    /// <remarks>
    /// Custom repositories must be thread-safe and provide atomic state transitions.
    /// </remarks>
    public ModuleJobSchedulerGuide UseCustomMetadataRepository<TRepository>()
        where TRepository : class, IMoJobMetadataRepository
    {
        PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IMoJobMetadataRepository, TRepository>();
        }, key: CONFIG_METADATA_STORE);
        return this;
    }

    /// <summary>
    /// Configures the module to use the in-memory metadata repository.
    /// </summary>
    public ModuleJobSchedulerGuide UseInMemoryMetadataRepository()
    {
        PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IMoJobMetadataRepository, InMemoryJobMetadataRepository>();
        }, key: CONFIG_METADATA_STORE);
        return this;
    }
    
    
    /// <summary>
    /// Configures the module to use the distributed event bus and cancellation manager providers.
    /// </summary>
    /// <returns></returns>
    public ModuleJobSchedulerGuide UseDistributeProvider()
    {
        ConfigureEmpty(CONFIG_PROVIDER);
        DependsOnModule<ModuleEventBusGuide>().Register()
            .AddKeyedCommonEventBus(nameof(ModuleJobScheduler), useDistributed: true);
        DependsOnModule<ModuleCancellationManagerGuide>().Register()
            .AddKeyedCancellationManager(nameof(ModuleJobScheduler), useDistributed: true);
        DependsOnModule<ModuleRegisterCentreGuide>().Register();
        return this;
    }
   
    /// <summary>
    /// Configures the module to use the in-memory metadata store and event bus and cancellation manager providers.
    /// </summary>
    /// <returns></returns>
    public ModuleJobSchedulerGuide UseInMemoryProvider()
    {
        ConfigureEmpty(CONFIG_PROVIDER);
        DependsOnModule<ModuleEventBusGuide>().Register()
            .AddKeyedCommonEventBus(nameof(ModuleJobScheduler), useDistributed: false);
        DependsOnModule<ModuleCancellationManagerGuide>().Register()
            .AddKeyedCancellationManager(nameof(ModuleJobScheduler), useDistributed: false);
        DependsOnModule<ModuleRegisterCentreGuide>().Register().UseInMemoryStateStore();
        return this;
    }
}

/// <summary>
/// Configuration options for the Job Scheduler module.
/// </summary>
public class ModuleJobSchedulerOption : MoModuleOption<ModuleJobScheduler>
{
    /// <summary>
    /// The project name used for job reconciliation and identification.
    /// Default: Entry Assembly Name.
    /// </summary>
    public string ProjectName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name
        ?? throw new InvalidOperationException("Entry assembly must have name for project identification.");

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
