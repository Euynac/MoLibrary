using Microsoft.Extensions.Logging;
using MoLibrary.StateStore.CancellationManager;
using MoLibrary.TaskScheduler.Abstractions;
using MoLibrary.TaskScheduler.ControlPlane;
using MoLibrary.TaskScheduler.Models;

namespace MoLibrary.TaskScheduler.Api;

/// <summary>
/// API service for task scheduler operations.
/// Provides methods for task management, history querying, and control operations.
/// </summary>
/// <remarks>
/// This service exposes task scheduler functionality via minimal APIs.
/// It serves as the interface layer between HTTP endpoints and the task scheduler components.
/// </remarks>
public class TaskSchedulerApiService(
    MoTaskScheduler taskScheduler,
    TaskRegistry taskRegistry,
    IMoTaskScheduleMetadataStore metadataStore,
    IMoCancellationManager cancellationManager,
    ILogger<TaskSchedulerApiService> logger)
{
    /// <summary>
    /// Gets all registered task definitions.
    /// </summary>
    public async Task<IEnumerable<TaskDefinition>> GetAllTasksAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("API: GetAllTasks requested");
        return await metadataStore.GetAllTaskDefinitionsAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a new task instance for manual execution.
    /// </summary>
    public async Task<string> CreateTaskInstanceAsync(
        string taskKey,
        string? parameters,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("API: CreateTaskInstance requested for {TaskKey}", taskKey);

        var definition = await taskRegistry.GetDefinitionAsync(taskKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Task {taskKey} not found");
        }

        // For now, use EnqueueAsync with parameters
        // This is a placeholder - actual implementation would need proper parameter handling
        throw new NotImplementedException("CreateTaskInstance API method not yet implemented");
    }

    /// <summary>
    /// Gets task execution history with filtering.
    /// </summary>
    public async Task<IEnumerable<TaskInstance>> GetTaskHistoryAsync(
        string? taskKey = null,
        TaskState? state = null,
        int? limit = 100,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("API: GetTaskHistory requested");

        // Placeholder - actual implementation would query metadata store with filters
        var allInstances = await metadataStore.GetAllTaskDefinitionsAsync(cancellationToken);
        return new List<TaskInstance>();
    }

    /// <summary>
    /// Cancels a running task instance.
    /// </summary>
    public async Task CancelTaskInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("API: CancelTaskInstance requested for {InstanceId}", instanceId);

        // Cancel the distributed cancellation token
        await cancellationManager.CancelTokenAsync(instanceId, cancellationToken);

        // Update instance state to Cancelled
        await metadataStore.UpdateTaskStateAsync(
            instanceId,
            TaskState.Cancelled,
            "Cancelled via API",
            cancellationToken);
    }

    /// <summary>
    /// Pauses a recurring task.
    /// </summary>
    public async Task PauseRecurringTaskAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("API: PauseRecurringTask requested for {TaskKey}", taskKey);
        await taskScheduler.PauseRecurringTaskAsync(taskKey, cancellationToken);
    }

    /// <summary>
    /// Resumes a paused recurring task.
    /// </summary>
    public async Task ResumeRecurringTaskAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("API: ResumeRecurringTask requested for {TaskKey}", taskKey);
        await taskScheduler.ResumeRecurringTaskAsync(taskKey, cancellationToken);
    }

    /// <summary>
    /// Updates task configuration.
    /// </summary>
    public async Task UpdateTaskConfigAsync(
        string taskKey,
        TaskDefinition updatedDefinition,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("API: UpdateTaskConfig requested for {TaskKey}", taskKey);

        // Update in metadata store
        await metadataStore.SaveTaskDefinitionAsync(updatedDefinition, cancellationToken);

        logger.LogInformation("Task {TaskKey} configuration updated", taskKey);
    }
}
