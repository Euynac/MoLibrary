using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Modules;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Cancellation.Abstractions;
using Monica.StateStore.TaskProgress.Abstractions;
using Monica.StateStore.TaskProgress.Models;
using TaskProgressModels = Monica.StateStore.TaskProgress.Models;

namespace Monica.StateStore.TaskProgress.Services;

/// <summary>
/// Provides task progress creation, persistence, and lifecycle management.
/// </summary>
public class TaskProgressService(
    [FromKeyedServices(nameof(ModuleTaskProgress))] IStateStore stateStore,
    [FromKeyedServices(nameof(ModuleTaskProgress))]
    ICancellationManager cancellationManager,
    ILogger<TaskProgressService> logger)
    : ITaskProgressService, IDisposable
{
    private const string SETTING_PREFIX = "TaskProgress:Setting:";
    private const string STATUS_PREFIX = "TaskProgress:Status:";

    private readonly ConcurrentDictionary<string, Timer> _autoUpdateTimers = new();

    public async Task<TaskProgressModels.TaskProgress> CreateTaskProgressAsync(string? id = null, Action<TaskProgressSetting>? settingAction = null)
    {
        return await CreateTaskProgressAsync<TaskProgressModels.TaskProgress>(id, settingAction);
    }

    public async Task<TaskProgressModels.TaskProgress?> FetchDistributedTaskProgress(string id)
    {
        return await FetchDistributedTaskProgress<TaskProgressModels.TaskProgress, TaskProgressStatus>(id);
    }

    public async Task<TCustomTaskProgress?> FetchDistributedTaskProgress<TCustomTaskProgress, TCustomStatus>(string id)
        where TCustomTaskProgress : TaskProgressModels.TaskProgress
        where TCustomStatus : TaskProgressStatus
    {
        var setting = await stateStore.GetStateAsync<TaskProgressSetting>(SETTING_PREFIX + id);
        if (setting == null) return null;

        var status = await GetTaskProgressStatusAsync<TCustomStatus>(id);
        var taskProgress = CreateTaskProgressInstance<TCustomTaskProgress>(setting, id);
        await InitializeTaskProgressAsync(taskProgress, status);
        taskProgress.DistributedStamp = GenerateDistributedStamp();
        return taskProgress;
    }

    /// <summary>
    /// Creates a task progress instance using a custom concrete type.
    /// </summary>
    public async Task<TCustomTaskProgress> CreateTaskProgressAsync<TCustomTaskProgress>(string? id = null, Action<TaskProgressSetting>? settingAction = null)
        where TCustomTaskProgress : TaskProgressModels.TaskProgress
    {
        var taskId = id ?? $"TaskProgress_{Guid.NewGuid()}";
        var setting = new TaskProgressSetting();
        settingAction?.Invoke(setting);

        try
        {
            var taskProgress = CreateTaskProgressInstance<TCustomTaskProgress>(setting, taskId);
            await InitializeTaskProgressAsync(taskProgress);

            if (setting.UseDistributedTaskProgress)
            {
                taskProgress.DistributedStamp = GenerateDistributedStamp();
            }

            // Save initial state
            await SaveTaskProgressStateAsync(taskProgress, saveInstantly: true);

            // Set up automatic updates
            if (setting.AutoUpdateDuration.HasValue)
            {
                SetupAutoUpdate(taskProgress, setting.AutoUpdateDuration.Value);
            }

            logger.LogInformation("Created task progress: {TaskId}", taskId);
            return taskProgress;
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to create task progress: {0}", taskId);
        }
    }

    /// <summary>
    /// Creates a progress-tracked task using the default <see cref="TaskProgress" /> model,
    /// resuming an already-running distributed progress with the same id instead of resetting it.
    /// </summary>
    public async Task<TaskProgressModels.TaskProgress> CreateOrResumeTaskProgressAsync(string? id = null, Action<TaskProgressSetting>? settingAction = null)
    {
        var taskId = id ?? $"TaskProgress_{Guid.NewGuid()}";

        // Resume an existing in-flight distributed progress so re-creation never resets the bar back to zero.
        var existing = await FetchDistributedTaskProgress(taskId);
        if (existing is { Status.IsEnd: false } && existing.Status.CurrentStep > 0)
        {
            if (existing.Setting.AutoUpdateDuration.HasValue)
            {
                SetupAutoUpdate(existing, existing.Setting.AutoUpdateDuration.Value);
            }

            // Persist the current position immediately and hand write ownership (DistributedStamp) to this instance.
            await SaveTaskProgressStateAsync(existing, saveInstantly: true);
            logger.LogInformation("Resumed task progress: {TaskId} at {CurrentStep}%", taskId, existing.Status.Percentage);
            return existing;
        }

        // Fresh task: reset any stale cancellation from a previous run so reusing the id is not blocked.
        try
        {
            await cancellationManager.ResetTokenAsync(taskId);
        }
        catch (Exception e)
        {
            e.CreateException(logger, "Failed to reset task progress cancellation token: {0}", taskId);
        }

        return await CreateTaskProgressAsync(taskId, settingAction);
    }

    public virtual string GenerateDistributedStamp()
    {
        return $"{Assembly.GetEntryAssembly()?.GetName()}-{Guid.NewGuid().ToString()}";
    }

    public async Task<CancellationToken> GetTaskProgressCancellationTokenAsync(string id)
    {
        return await cancellationManager.GetOrCreateTokenAsync(id);
    }

    /// <summary>
    /// Retrieves the persisted task progress status.
    /// </summary>
    public async Task<TaskProgressStatus?> GetTaskProgressStatusAsync(string id)
    {
        try
        {
            var status = await stateStore.GetStateAsync<TaskProgressStatus>(STATUS_PREFIX + id);
            return status;
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to get task progress status: {0}", id);
        }
    }

    /// <summary>
    /// Retrieves the persisted custom status for the specified task progress.
    /// </summary>
    /// <typeparam name="TStatus">The custom status type.</typeparam>
    /// <param name="id">The task identifier.</param>
    /// <returns>The persisted custom status, or <see langword="null" /> when it does not exist.</returns>
    public async Task<TStatus?> GetTaskProgressStatusAsync<TStatus>(string id) where TStatus : TaskProgressStatus
    {
        try
        {
            var status = await stateStore.GetStateAsync<TStatus>(STATUS_PREFIX + id);
            return status;
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to get custom task progress status: {0}", id);
        }
    }

    public async ValueTask SaveTaskProgressStateAsync(TaskProgressModels.TaskProgress taskProgress, bool saveInstantly = false,
        bool isComplete = false)
    {
        try
        {
            if (taskProgress.Setting.UseDistributedTaskProgress)
            {
                taskProgress.Setting.DistributedStamp = taskProgress.DistributedStamp;
                await stateStore.SaveStateAsync(SETTING_PREFIX + taskProgress.TaskId, taskProgress.Setting, ttl: taskProgress.Setting.TimeToLive);
            }

            // Skip saving if auto-update is set and does not require immediate saving
            if (taskProgress.Setting.AutoUpdateDuration.HasValue && !saveInstantly)
            {
                logger.LogTrace("Skipped saving task progress due to deferred auto-update: {TaskId}", taskProgress.TaskId);
                return;
            }

            await stateStore.SaveStateAsync(STATUS_PREFIX + taskProgress.TaskId, taskProgress.Status, ttl: isComplete ? taskProgress.Setting.CompletedTimeToLive : taskProgress.Setting.TimeToLive);

            logger.LogDebug("Saved task progress: {TaskId}, Progress: {Progress}%",
                taskProgress.TaskId, taskProgress.Status.Percentage);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to save task progress state: {0}", taskProgress.TaskId);
        }
    }

    /// <summary>
    /// Completes a task progress instance and persists the final state.
    /// </summary>
    public async Task FinishTaskProgressAsync(TaskProgressModels.TaskProgress taskProgress)
    {
        try
        {
            // Stop automatic updates
            StopAutoUpdate(taskProgress.TaskId);

            await SaveTaskProgressStateAsync(taskProgress, true, true);

            // Delete the cancellation token from the cancellation manager.
            await cancellationManager.DeleteTokenAsync(taskProgress.TaskId);

            logger.LogInformation("Finished task progress: {TaskId}", taskProgress.TaskId);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to finish task progress: {0}", taskProgress.TaskId);
        }
    }

    /// <summary>
    /// Cancels a task progress instance and persists the final state.
    /// </summary>
    public async Task CancelTaskProgressAsync(TaskProgressModels.TaskProgress taskProgress, string? reason = null)
    {
        try
        {
            // Stop automatic updates
            StopAutoUpdate(taskProgress.TaskId);

            // Send cancellation signal via cancellation manager
            await cancellationManager.CancelTokenAsync(taskProgress.TaskId);

            await SaveTaskProgressStateAsync(taskProgress, true, true);

            logger.LogInformation("Cancelled task progress: {TaskId}, Reason: {Reason}", taskProgress.TaskId, reason);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to cancel task progress: {0}, Reason: {1}", taskProgress.TaskId, reason);
        }
    }

    /// <summary>
    /// Set up automatic updates
    /// </summary>
    private void SetupAutoUpdate(TaskProgressModels.TaskProgress taskProgress, TimeSpan interval)
    {
        var timer = new Timer(async _ => await AutoUpdateCallback(taskProgress), null, interval, interval);
        if (_autoUpdateTimers.TryAdd(taskProgress.TaskId, timer))
        {
            logger.LogDebug("Configured auto-update for task progress: {TaskId}, Interval: {Interval}", taskProgress.TaskId, interval);
            return;
        }

        timer.Dispose();
    }

    /// <summary>
    /// Stop automatic updates
    /// </summary>
    private void StopAutoUpdate(string taskId)
    {
        if (_autoUpdateTimers.TryRemove(taskId, out var timer))
        {
            timer.Dispose();
            logger.LogDebug("Stopped auto-update for task progress: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Performs a timed task progress persistence update.
    /// </summary>
    private async Task AutoUpdateCallback(TaskProgressModels.TaskProgress taskProgress)
    {
        // Stop automatic updates if task is completed or canceled
        if (taskProgress.IsCompleted || taskProgress.IsCancelled)
        {
            StopAutoUpdate(taskProgress.TaskId);
            return;
        }

        if (!taskProgress.IsCurrentTurn)
        {
            return;
        }

        try
        {
            // Force save during automatic update
            await SaveTaskProgressStateAsync(taskProgress, true);

            logger.LogTrace("Auto-updated task progress: {TaskId}, Progress: {Progress}%",
                taskProgress.TaskId, taskProgress.Status.Percentage);
        }
        catch (Exception e)
        {
            e.CreateException(logger, "Auto update failed for task progress: {0}", taskProgress.TaskId);
        }
    }

    public void Dispose()
    {
        foreach (var timer in _autoUpdateTimers.Values)
        {
            timer.Dispose();
        }
        _autoUpdateTimers.Clear();
    }

    private TTaskProgress CreateTaskProgressInstance<TTaskProgress>(TaskProgressSetting setting, string taskId)
        where TTaskProgress : TaskProgressModels.TaskProgress
    {
        return (TTaskProgress)Activator.CreateInstance(typeof(TTaskProgress), setting, this, taskId)!;
    }

    private async Task InitializeTaskProgressAsync(TaskProgressModels.TaskProgress taskProgress, object? distributedStatus = null)
    {
        var cancellationToken = await cancellationManager.GetOrCreateTokenAsync(taskProgress.TaskId);
        taskProgress.SetCancellationToken(cancellationToken);
        taskProgress.InitTaskProgressStatus(distributedStatus);
    }
}
