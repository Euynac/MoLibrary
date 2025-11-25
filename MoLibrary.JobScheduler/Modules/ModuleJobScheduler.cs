using System.Reflection;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Attributes;
using MoLibrary.JobScheduler.Cache;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.HealthChecks;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.WorkerPlane;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Main module implementation for the Job Scheduler system.
/// Integrates all components including control plane (scheduling), worker plane (execution),
/// and metadata persistence layer.
/// </summary>
public class ModuleJobScheduler(ModuleJobSchedulerOption option)
    : MoModuleWithDependencies<ModuleJobScheduler, ModuleJobSchedulerOption, ModuleJobSchedulerGuide>(option), IWantIterateBusinessTypes
{
    private readonly List<JobDefinition> _jobDefinitions = [];

    public override EMoModules CurModuleEnum() => EMoModules.JobScheduler;

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
                else if (type.IsImplementInterfaceGeneric(typeof(IMoTriggeredJob<>), out var argsType))
                {
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

        Logger.LogInformation("Discovered {Count} job type(s) for registration", _jobDefinitions.Count);

        if (_jobDefinitions.Count == 0)
        {
            Logger.LogInformation("No jobs discovered. Job scheduler will run without any registered jobs.");
            return;
        }

        services.AddSingleton<JobExecutor>();
        services.AddSingleton<JobRegistry>();
        services.AddSingleton<JobInstanceManager>();
        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<IJobDefinitionCacheService, JobDefinitionCacheServiceDisabled>();
        services.AddSingleton<JobOrchestrator>();

        services.AddSingleton<IJobConcurrencyGuard, JobConcurrencyGuardHostedService>();
        
        services.AddHostedService<JobRegistrationHostedService>(provider => ActivatorUtilities.CreateInstance<JobRegistrationHostedService>(provider, _jobDefinitions));

        services.AddHostedService<JobWorkerManager>();

        if (GetOptions<ModuleRegisterCentreOption>().IsCentreServer)
        {
            services.AddHostedService<JobSchedulerHostedService>();
            services.AddHostedService(provider => provider.GetRequiredService<IJobConcurrencyGuard>() as JobConcurrencyGuardHostedService
                                                  ?? throw new InvalidOperationException("JobConcurrencyGuard must be registered as IJobConcurrencyGuard"));
        }

        // Register health check for monitoring initialization status
        services.AddHealthChecks()
            .AddCheck<JobSchedulerHealthCheck>("job-scheduler", tags: ["ready", "scheduler"]);
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
            FromProject = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new InvalidOperationException("Entry assembly must have name for getting job source project.")
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
                    CronExpression.Parse(definition.CronExpression, CronFormat.IncludeSeconds);
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
        
       
    }
}
