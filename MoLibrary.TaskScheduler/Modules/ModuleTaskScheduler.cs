using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.TaskScheduler.Attributes;
using MoLibrary.TaskScheduler.ControlPlane;
using MoLibrary.TaskScheduler.Models;
using MoLibrary.TaskScheduler.Tasks;

namespace MoLibrary.TaskScheduler.Modules;

/// <summary>
/// Main module implementation for the Task Scheduler system.
/// Integrates all components including control plane (scheduling), worker plane (execution),
/// and metadata persistence layer.
/// </summary>
public class ModuleTaskScheduler(ModuleTaskSchedulerOption option)
    : MoModule<ModuleTaskScheduler, ModuleTaskSchedulerOption, ModuleTaskSchedulerGuide>(option), IWantIterateBusinessTypes
{
    private readonly List<Type> _taskTypes = [];
    private readonly List<TaskDefinition> _taskDefinitions = [];

    public override EMoModules CurModuleEnum() => EMoModules.TaskScheduler;

    /// <summary>
    /// Iterates through business types to discover and collect task types.
    /// Extracts metadata from TaskConfigAttribute and creates TaskDefinition objects.
    /// </summary>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false })
            {
                // Check if type inherits from RecurringTask
                if (type.IsAssignableTo(typeof(RecurringTask)))
                {
                    try
                    {
                        var taskDefinition = ExtractTaskDefinition(type, TaskType.Recurring);
                        _taskTypes.Add(type);
                        _taskDefinitions.Add(taskDefinition);
                        Logger.LogDebug("Discovered RecurringTask: {TaskKey} ({TypeName})", taskDefinition.TaskKey, type.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to extract task definition from RecurringTask type: {TypeName}", type.FullName);
                    }
                }
                // Check if type inherits from TriggeredTask<>
                else if (IsTriggeredTask(type))
                {
                    try
                    {
                        var taskDefinition = ExtractTaskDefinition(type, TaskType.Triggered);
                        _taskTypes.Add(type);
                        _taskDefinitions.Add(taskDefinition);
                        Logger.LogDebug("Discovered TriggeredTask: {TaskKey} ({TypeName})", taskDefinition.TaskKey, type.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to extract task definition from TriggeredTask type: {TypeName}", type.FullName);
                    }
                }
            }

            yield return type;
        }
    }

    /// <summary>
    /// Registers all discovered task types to DI and registers task definitions to TaskRegistry.
    /// </summary>
    public override void PostConfigureServices(IServiceCollection services)
    {
        // Register all discovered task types as transient in DI
        foreach (var taskType in _taskTypes)
        {
            services.AddTransient(taskType);
            Logger.LogDebug("Registered task type to DI: {TypeName}", taskType.Name);
        }

        Logger.LogInformation("Discovered {Count} task type(s) for registration", _taskDefinitions.Count);

        if (_taskDefinitions.Count == 0)
        {
            Logger.LogInformation("No tasks discovered. Task scheduler will run without any registered tasks.");
            return;
        }

        // Register task definitions to TaskRegistry
        // This is done synchronously during startup (blocking is acceptable)
        services.AddHostedService<TaskRegistrationHostedService>(provider =>
        {
            var taskRegistry = provider.GetRequiredService<TaskRegistry>();
            var logger = provider.GetRequiredService<ILogger<TaskRegistrationHostedService>>();
            return new TaskRegistrationHostedService(taskRegistry, _taskDefinitions, logger);
        });
    }

    /// <summary>
    /// Checks if a type inherits from TriggeredTask{TParam}.
    /// </summary>
    private static bool IsTriggeredTask(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(TriggeredTask<>))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Extracts TaskDefinition from a task type using reflection and TaskConfigAttribute.
    /// </summary>
    private TaskDefinition ExtractTaskDefinition(Type taskType, TaskType taskTypeEnum)
    {
        // Extract TaskConfigAttribute if present
        var attribute = taskType.GetCustomAttributes(typeof(TaskConfigAttribute), false)
            .FirstOrDefault() as TaskConfigAttribute;

        // Create TaskDefinition with defaults
        var definition = new TaskDefinition
        {
            TaskKey = attribute?.TaskKey ?? taskType.FullName ?? taskType.Name,
            TaskName = attribute?.TaskName ?? taskType.Name,
            Description = attribute?.Description,
            Type = taskTypeEnum,
            MaxConcurrency = attribute?.MaxConcurrencyBridge ?? 1,
            RetryCount = attribute?.RetryCountBridge ?? 0,
            MaxExecutionTimeout = attribute?.MaxExecutionTimeout ?? TimeSpan.FromHours(1),
            IsDisabled = attribute?.IsDisabledBridge ?? false,
            TaskClrType = taskType
        };

        // Extract recurring task specific properties
        if (taskTypeEnum == TaskType.Recurring)
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
                    Logger.LogError(ex, "Invalid cron expression '{CronExpression}' for task {TaskKey}. Task will be disabled.",
                        definition.CronExpression, definition.TaskKey);
                    definition.IsDisabled = true;
                }
            }
            else
            {
                Logger.LogWarning("RecurringTask {TaskKey} has no cron expression defined. Task will be disabled.", definition.TaskKey);
                definition.IsDisabled = true;
            }
        }

        // Extract triggered task parameter type
        if (taskTypeEnum == TaskType.Triggered)
        {
            var baseType = taskType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(TriggeredTask<>))
                {
                    definition.ParameterClrType = baseType.GetGenericArguments()[0];
                    break;
                }
                baseType = baseType.BaseType;
            }

            if (definition.ParameterClrType == null)
            {
                Logger.LogWarning("Failed to extract parameter type from TriggeredTask {TaskKey}", definition.TaskKey);
            }
        }

        return definition;
    }
}
