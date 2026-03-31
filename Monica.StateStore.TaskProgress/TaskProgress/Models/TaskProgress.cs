using Monica.StateStore.TaskProgress.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.StateStore.TaskProgress.Models;

/// <summary>
/// Base class for progress-tracked tasks with a custom status payload.
/// </summary>
/// <typeparam name="TCustomStatus">The concrete status type managed by this task progress instance.</typeparam>
public abstract class TaskProgress<TCustomStatus>(
    TaskProgressSetting setting,
    ITaskProgressService service,
    string taskId)
    : TaskProgress(setting, service, taskId)
    where TCustomStatus : TaskProgressStatus
{
    private TCustomStatus? _customStatus;

    /// <summary>
    /// Gets the status payload as the custom status type.
    /// </summary>
    public override TaskProgressStatus Status
    {
        get => CustomStatus;
        protected set
        {
            if (value is TCustomStatus customStatus)
            {
                CustomStatus = customStatus;
                return;
            }

            throw new InvalidOperationException(
                $"Can not set origin status when using custom status property in {GetType().GetCleanFullName()}!");
        }
    }

    /// <summary>
    /// Gets the strongly typed custom status payload.
    /// </summary>
    public TCustomStatus CustomStatus
    {
        get => _customStatus ?? throw new InvalidOperationException(
            $"The progress status has not been initialized. Call {nameof(InitTaskProgressStatus)} first.");
        protected set => _customStatus = value;
    }

    /// <summary>
    /// Creates the default custom status payload for a new task progress instance.
    /// </summary>
    public abstract TCustomStatus CreateDefaultCustomStatus();

    /// <inheritdoc />
    public override void InitTaskProgressStatus(object? distributedStatus = null)
    {
        CustomStatus = distributedStatus switch
        {
            null => CreateDefaultCustomStatus(),
            TCustomStatus status => status,
            _ => throw new InvalidOperationException(
                $"{distributedStatus.GetType().GetCleanFullName()} is not a valid {typeof(TCustomStatus).Name} instance for task progress initialization.")
        };
    }
}

/// <summary>
/// Tracks and manages the execution progress of a task.
/// </summary>
/// <param name="setting">The behavior and persistence settings for this task.</param>
/// <param name="service">The service responsible for persistence and lifecycle operations.</param>
/// <param name="taskId">The unique task identifier.</param>
public class TaskProgress(TaskProgressSetting setting, ITaskProgressService service, string taskId)
{
    private bool _isCancelled;
    private CancellationToken? _cancellationToken;

    /// <summary>
    /// Gets the unique task identifier.
    /// </summary>
    public string TaskId { get; } = taskId;

    /// <summary>
    /// Gets the behavior and persistence settings for this task.
    /// </summary>
    public TaskProgressSetting Setting { get; } = setting;

    private TaskProgressStatus? _status;

    /// <summary>
    /// Gets the current progress status.
    /// </summary>
    public virtual TaskProgressStatus Status
    {
        get => _status ?? throw new InvalidOperationException(
            $"The progress status has not been initialized. Call {nameof(InitTaskProgressStatus)} first.");
        protected set => _status = value;
    }

    /// <summary>
    /// Has the task been completed?
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the task has been cancelled.
    /// </summary>
    public bool IsCancelled => _isCancelled || (_cancellationToken?.IsCancellationRequested ?? false);

    /// <summary>
    /// Gets the cancellation token associated with this task.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Gets the service responsible for persistence and lifecycle operations.
    /// </summary>
    protected ITaskProgressService Service { get; } = service;

    /// <summary>
    /// Raised when the task status changes.
    /// </summary>
    public event EventHandler<TaskProgressEventArgs>? StatusUpdated;

    /// <summary>
    /// Raised when the task is cancelled.
    /// </summary>
    public event EventHandler<TaskProgressCancelledEventArgs>? Cancelled;

    /// <summary>
    /// Raised when the task is completed.
    /// </summary>
    public event EventHandler<TaskProgressEventArgs>? Completed;

    /// <summary>
    /// Initializes the task progress state.
    /// </summary>
    /// <param name="distributedStatus">Optional distributed status snapshot restored from persistent storage.</param>
    public virtual void InitTaskProgressStatus(object? distributedStatus = null)
    {
        _status = distributedStatus switch
        {
            null => new TaskProgressStatus(Setting.TotalSteps, TaskId),
            TaskProgressStatus status => status,
            _ => throw new InvalidOperationException(
                $"{distributedStatus.GetType().GetCleanFullName()} is not a valid {nameof(TaskProgressStatus)} instance for task progress initialization.")
        };
    }

    /// <summary>
    /// Gets or sets the distributed ownership stamp of the current writer.
    /// </summary>
    public string? DistributedStamp { get; set; }

    /// <summary>
    /// Indicates whether the current process still owns writes for the distributed task.
    /// </summary>
    public bool IsCurrentTurn => Setting.DistributedStamp == DistributedStamp;

    /// <summary>
    /// Associates a cancellation token with the task and listens for external cancellation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    internal void SetCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                if (IsCompleted || _isCancelled) return;

                _isCancelled = true;
                OnCancelled(new TaskProgressCancelledEventArgs(this, "Task was cancelled externally."));
            });
        }
    }

    /// <summary>
    /// Raises the status update event.
    /// </summary>
    /// <param name="e">The event payload.</param>
    protected virtual void OnStatusUpdated(TaskProgressEventArgs e)
    {
        StatusUpdated?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the cancellation event.
    /// </summary>
    /// <param name="e">The event payload.</param>
    protected virtual void OnCancelled(TaskProgressCancelledEventArgs e)
    {
        Cancelled?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the completion event.
    /// </summary>
    /// <param name="e">The event payload.</param>
    protected virtual void OnCompleted(TaskProgressEventArgs e)
    {
        Completed?.Invoke(this, e);
    }

    /// <summary>
    /// Persists the current task progress status.
    /// </summary>
    /// <param name="saveInstantly">Whether to save immediately. When auto-update is configured and this is <see langword="false" />, the save is deferred.</param>
    /// <returns>A value task that completes when persistence finishes.</returns>
    public virtual async ValueTask SaveStatus(bool saveInstantly = false)
    {
        if (IsCompleted || IsCancelled) return;

        Status.LastUpdated = DateTime.Now;
        await Service.SaveTaskProgressStateAsync(this, saveInstantly);

        OnStatusUpdated(new TaskProgressEventArgs(this));
    }

    /// <summary>
    /// Updates the current task progress status.
    /// </summary>
    /// <param name="currentStep">The current step number will ensure that it is not less than 0</param>
    /// <param name="statusMessage">status message</param>
    /// <param name="phase">Current stage (optional)</param>
    /// <returns>Asynchronous tasks</returns>
    public virtual async Task UpdateStatusAsync(int currentStep, string? statusMessage = null, string? phase = null)
    {
        ThrowIfCancellationRequested();
        if (IsCompleted || IsCancelled) return;

        Status.CurrentStep = Math.Max(0, currentStep);
        ApplyStatusMetadata(statusMessage, phase);
        await SaveStatus();
    }

    /// <summary>
    /// Increments task progress.
    /// </summary>
    /// <param name="increment">Increment the number of steps, default is 1</param>
    /// <param name="statusMessage">status message</param>
    /// <param name="phase">Current stage (optional)</param>
    /// <returns>Asynchronous tasks</returns>
    public virtual async Task IncrementAsync(int increment = 1, string? statusMessage = null, string? phase = null)
    {
        await UpdateStatusAsync(Status.CurrentStep + increment, statusMessage, phase);
    }

    /// <summary>
    /// Updates the current task phase.
    /// </summary>
    /// <param name="phase">Stage name</param>
    /// <param name="statusMessage">Status message (optional)</param>
    /// <returns>Asynchronous tasks</returns>
    public virtual async Task UpdatePhaseAsync(string phase, string? statusMessage = null)
    {
        ThrowIfCancellationRequested();
        if (IsCompleted || IsCancelled) return;

        ApplyStatusMetadata(statusMessage, phase);
        await SaveStatus();
    }

    /// <summary>
    /// Completes the task and persists the final status.
    /// </summary>
    /// <returns>A task that completes when final persistence finishes.</returns>
    public virtual async Task CompleteTaskAsync(string? statusMessage = null, string? phase = null)
    {
        if (IsCompleted || IsCancelled) return;

        ApplyStatusMetadata(statusMessage, phase);
        IsCompleted = true;
        Status.CurrentStep = Status.TotalSteps;
        await Service.FinishTaskProgressAsync(this);

        OnCompleted(new TaskProgressEventArgs(this));
    }

    /// <summary>
    /// Cancels the task.
    /// </summary>
    /// <param name="reason">The optional human-readable cancellation reason.</param>
    /// <returns>A task that completes when cancellation persistence finishes.</returns>
    public virtual async Task CancelTaskAsync(string? reason = null)
    {
        if (IsCompleted || _isCancelled) return;

        _isCancelled = true;
        Status.IsCancelled = true;
        if (!string.IsNullOrEmpty(reason))
        {
            Status.CancelReason = reason;
        }

        await Service.CancelTaskProgressAsync(this, reason);

        OnCancelled(new TaskProgressCancelledEventArgs(this, reason));
    }

    /// <summary>
    /// Throws if the task has already been cancelled.
    /// </summary>
    public virtual void ThrowIfCancellationRequested()
    {
        if (IsCancelled)
        {
            throw new InvalidOperationException($"Task progress '{TaskId}' has been cancelled.");
        }

        _cancellationToken?.ThrowIfCancellationRequested();
    }

    private void ApplyStatusMetadata(string? statusMessage = null, string? phase = null)
    {
        if (!string.IsNullOrEmpty(statusMessage))
        {
            Status.CurrentStatus = statusMessage;
        }

        if (!string.IsNullOrEmpty(phase))
        {
            Status.Phase = phase;
        }
    }
}
