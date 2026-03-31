using Monica.StateStore.TaskProgress.Models;
using TaskProgressModels = Monica.StateStore.TaskProgress.Models;

namespace Monica.StateStore.TaskProgress.Abstractions;

/// <summary>
/// Provides creation, persistence, and lifecycle operations for progress-tracked tasks.
/// </summary>
public interface ITaskProgressService
{
    /// <summary>
    /// Creates a progress-tracked task using the default <see cref="TaskProgress" /> model.
    /// </summary>
    /// <param name="id">Optional explicit task identifier. When omitted, a GUID-based identifier is generated.</param>
    /// <param name="settingAction">Optional callback used to customize the task progress settings before initialization.</param>
    /// <returns>The initialized task progress instance.</returns>
    Task<TaskProgressModels.TaskProgress> CreateTaskProgressAsync(string? id = null, Action<TaskProgressSetting>? settingAction = null);

    /// <summary>
    /// Restores a distributed progress-tracked task using the default <see cref="TaskProgress" /> model.
    /// </summary>
    /// <param name="id">The identifier of the distributed task.</param>
    /// <returns>The restored task progress, or <see langword="null" /> when no distributed state exists.</returns>
    Task<TaskProgressModels.TaskProgress?> FetchDistributedTaskProgress(string id);

    /// <summary>
    /// Creates a progress-tracked task using a custom <see cref="TaskProgress" /> subtype.
    /// </summary>
    /// <typeparam name="TCustomTaskProgress">The concrete task progress type to instantiate.</typeparam>
    /// <param name="id">Optional explicit task identifier. When omitted, a GUID-based identifier is generated.</param>
    /// <param name="settingAction">Optional callback used to customize the task progress settings before initialization.</param>
    /// <returns>The initialized custom task progress instance.</returns>
    Task<TCustomTaskProgress> CreateTaskProgressAsync<TCustomTaskProgress>(string? id = null, Action<TaskProgressSetting>? settingAction = null)
        where TCustomTaskProgress : TaskProgressModels.TaskProgress;

    /// <summary>
    /// Restores a distributed progress-tracked task using custom task progress and status types.
    /// </summary>
    /// <typeparam name="TCustomTaskProgress">The concrete task progress type to instantiate.</typeparam>
    /// <typeparam name="TCustomStatus">The concrete status payload type stored for the task.</typeparam>
    /// <param name="id">The identifier of the distributed task.</param>
    /// <returns>The restored custom task progress, or <see langword="null" /> when no distributed state exists.</returns>
    Task<TCustomTaskProgress?> FetchDistributedTaskProgress<TCustomTaskProgress, TCustomStatus>(string id)
        where TCustomTaskProgress : TaskProgressModels.TaskProgress
        where TCustomStatus : TaskProgressStatus;

    /// <summary>
    /// Resolves the cancellation token associated with the specified task.
    /// </summary>
    /// <param name="id">The unique task identifier.</param>
    /// <returns>The cancellation token associated with the task.</returns>
    Task<CancellationToken> GetTaskProgressCancellationTokenAsync(string id);

    /// <summary>
    /// Retrieves the persisted status payload for the specified task.
    /// </summary>
    /// <param name="id">The unique task identifier.</param>
    /// <returns>The persisted status payload, or <see langword="null" /> when no status exists.</returns>
    Task<TaskProgressStatus?> GetTaskProgressStatusAsync(string id);

    /// <summary>
    /// Retrieves the persisted custom status payload for the specified task.
    /// </summary>
    /// <typeparam name="TCustomStatus">Custom status type</typeparam>
    /// <param name="id">The unique task identifier.</param>
    /// <returns>The persisted custom status payload, or <see langword="null" /> when no status exists.</returns>
    Task<TCustomStatus?> GetTaskProgressStatusAsync<TCustomStatus>(string id) where TCustomStatus : TaskProgressStatus;

    /// <summary>
    /// Persists the current state of a progress-tracked task.
    /// </summary>
    /// <param name="taskProgress">The task progress instance to persist.</param>
    /// <param name="saveInstantly">When <see langword="true" />, bypasses deferred auto-update persistence and writes immediately.</param>
    /// <param name="isComplete">Indicates whether the save should use the completion retention policy.</param>
    /// <returns>A value task that completes when persistence finishes.</returns>
    ValueTask SaveTaskProgressStateAsync(TaskProgressModels.TaskProgress taskProgress, bool saveInstantly = false,
        bool isComplete = false);

    /// <summary>
    /// Marks the specified task as completed and persists its final state.
    /// </summary>
    /// <param name="taskProgress">The task progress instance to complete.</param>
    /// <returns>A task that completes when final persistence finishes.</returns>
    Task FinishTaskProgressAsync(TaskProgressModels.TaskProgress taskProgress);

    /// <summary>
    /// Cancels the specified task and persists its final state.
    /// </summary>
    /// <param name="taskProgress">The task progress instance to cancel.</param>
    /// <param name="reason">An optional human-readable cancellation reason.</param>
    /// <returns>A task that completes when cancellation persistence finishes.</returns>
    Task CancelTaskProgressAsync(TaskProgressModels.TaskProgress taskProgress, string? reason = null);
}
