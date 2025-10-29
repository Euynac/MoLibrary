using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoLibrary.TaskScheduler.ControlPlane;
using MoLibrary.TaskScheduler.Exceptions;
using MoLibrary.TaskScheduler.Models;

namespace MoLibrary.TaskScheduler.Modules;

/// <summary>
/// Hosted service that registers discovered task definitions to the TaskRegistry during application startup.
/// Ensures all tasks are properly registered before the application begins serving requests.
/// </summary>
internal class TaskRegistrationHostedService(
    TaskRegistry taskRegistry,
    IReadOnlyList<TaskDefinition> taskDefinitions,
    ILogger<TaskRegistrationHostedService> logger) : IHostedService
{
    /// <summary>
    /// Registers all discovered task definitions when the application starts.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (taskDefinitions.Count == 0)
        {
            logger.LogInformation("No task definitions to register");
            return;
        }

        try
        {
            logger.LogInformation("Starting task registration for {Count} task(s)", taskDefinitions.Count);

            // Extract task keys from definitions
            var taskKeys = taskDefinitions.Select(d => d.TaskKey).ToList();

            // Check which tasks are not yet registered
            var unregisteredKeys = await taskRegistry.CheckUnregisteredAsync(taskKeys, cancellationToken);
            var unregisteredKeysList = unregisteredKeys.ToList();

            if (unregisteredKeysList.Count == 0)
            {
                logger.LogInformation("All {Count} task(s) are already registered. Skipping registration.", taskDefinitions.Count);
                return;
            }

            // Filter to only unregistered task definitions
            var unregisteredDefinitions = taskDefinitions
                .Where(d => unregisteredKeysList.Contains(d.TaskKey))
                .ToList();

            logger.LogInformation("Registering {UnregisteredCount} unregistered task(s) out of {TotalCount} discovered",
                unregisteredDefinitions.Count, taskDefinitions.Count);

            // Register unregistered tasks
            await taskRegistry.RegisterTasksAsync(unregisteredDefinitions, cancellationToken);

            logger.LogInformation("Successfully registered {Count} task definition(s)", unregisteredDefinitions.Count);

            // Log summary of all discovered tasks
            foreach (var definition in taskDefinitions)
            {
                var status = unregisteredKeysList.Contains(definition.TaskKey) ? "Registered" : "Already Registered";
                logger.LogInformation(
                    "[{Status}] Task: {TaskKey} | Name: {TaskName} | Type: {TaskType} | MaxConcurrency: {MaxConcurrency} | RetryCount: {RetryCount} | Timeout: {Timeout}",
                    status,
                    definition.TaskKey,
                    definition.TaskName,
                    definition.Type,
                    definition.MaxConcurrency,
                    definition.RetryCount,
                    definition.MaxExecutionTimeout);

                if (definition.Type == TaskType.Recurring)
                {
                    logger.LogDebug(
                        "Recurring task details - TaskKey: {TaskKey}, CronExpression: {CronExpression}, StartTime: {StartTime}, EndTime: {EndTime}, IsDisabled: {IsDisabled}",
                        definition.TaskKey,
                        definition.CronExpression,
                        definition.StartTime,
                        definition.EndTime,
                        definition.IsDisabled);
                }
                else if (definition.Type == TaskType.Triggered)
                {
                    logger.LogDebug(
                        "Triggered task details - TaskKey: {TaskKey}, ParameterType: {ParameterType}",
                        definition.TaskKey,
                        definition.ParameterClrType?.FullName ?? "None");
                }
            }
        }
        catch (TaskRegistrationException ex)
        {
            logger.LogError(ex,
                "Task registration failed for task key: {TaskKey}. Message: {Message}",
                ex.TaskKey,
                ex.Message);
            throw; // Re-throw to prevent application startup if registration fails
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during task registration");
            throw; // Re-throw to prevent application startup if registration fails
        }
    }

    /// <summary>
    /// No cleanup needed when the application stops.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }
}
