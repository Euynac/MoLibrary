using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.JobScheduler.Attributes;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Jobs;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Main module implementation for the Job Scheduler system.
/// Integrates all components including control plane (scheduling), worker plane (execution),
/// and metadata persistence layer.
/// </summary>
public class ModuleJobScheduler(ModuleJobSchedulerOption option)
    : MoModule<ModuleJobScheduler, ModuleJobSchedulerOption, ModuleJobSchedulerGuide>(option), IWantIterateBusinessTypes
{
    private readonly List<Type> _jobTypes = [];
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
                // Check if type inherits from RecurringTask
                if (type.IsAssignableTo(typeof(RecurringJob)))
                {
                    try
                    {
                        var jobDefinition = ExtractJobDefinition(type, JobType.Recurring);
                        _jobTypes.Add(type);
                        _jobDefinitions.Add(jobDefinition);
                        Logger.LogDebug("Discovered RecurringJob: {JobKey} ({TypeName})", jobDefinition.JobKey, type.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to extract job definition from RecurringJob type: {TypeName}", type.FullName);
                    }
                }
                // Check if type inherits from TriggeredJob<>
                else if (IsTriggeredJob(type))
                {
                    try
                    {
                        var jobDefinition = ExtractJobDefinition(type, JobType.Triggered);
                        _jobTypes.Add(type);
                        _jobDefinitions.Add(jobDefinition);
                        Logger.LogDebug("Discovered TriggeredJob: {JobKey} ({TypeName})", jobDefinition.JobKey, type.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to extract job definition from TriggeredJob type: {TypeName}", type.FullName);
                    }
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
        foreach (var jobType in _jobTypes)
        {
            services.AddTransient(jobType);
            Logger.LogDebug("Registered job type to DI: {TypeName}", jobType.Name);
        }

        Logger.LogInformation("Discovered {Count} job type(s) for registration", _jobDefinitions.Count);

        if (_jobDefinitions.Count == 0)
        {
            Logger.LogInformation("No jobs discovered. Job scheduler will run without any registered jobs.");
            return;
        }

        // Register job definitions to JobRegistry
        // This is done synchronously during startup (blocking is acceptable)
        services.AddHostedService<JobRegistrationHostedService>(provider =>
        {
            var jobRegistry = provider.GetRequiredService<JobRegistry>();
            var logger = provider.GetRequiredService<ILogger<JobRegistrationHostedService>>();
            return new JobRegistrationHostedService(jobRegistry, _jobDefinitions, logger);
        });
    }

    /// <summary>
    /// Checks if a type inherits from TriggeredJob{TParam}.
    /// </summary>
    private static bool IsTriggeredJob(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(MoTriggeredJob<>))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
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
            JobKey = attribute?.JobKey ?? jobType.FullName ?? jobType.Name,
            JobName = attribute?.JobName ?? jobType.Name,
            Description = attribute?.Description,
            Type = jobTypeEnum,
            MaxConcurrency = attribute?.MaxConcurrencyBridge ?? 1,
            RetryCount = attribute?.RetryCountBridge ?? 0,
            MaxExecutionTimeout = attribute?.MaxExecutionTimeout ?? TimeSpan.FromHours(1),
            IsDisabled = attribute?.IsDisabledBridge ?? false,
            JobClrType = jobType
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

        // Extract triggered job parameter type
        if (jobTypeEnum == JobType.Triggered)
        {
            var baseType = jobType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(MoTriggeredJob<>))
                {
                    definition.ParameterClrType = baseType.GetGenericArguments()[0];
                    break;
                }
                baseType = baseType.BaseType;
            }

            if (definition.ParameterClrType == null)
            {
                Logger.LogWarning("Failed to extract parameter type from TriggeredJob {JobKey}", definition.JobKey);
            }
        }

        return definition;
    }
}
