using Monica.Tool.Extensions;

namespace Monica.StateStore.ProgressBar;

/// <summary>
/// Custom progress bar class
/// </summary>
/// <typeparam name="TCustomStatus"></typeparam>
public abstract class ProgressBar<TCustomStatus>(
    ProgressBarSetting setting,
    IMoProgressBarService service,
    string taskId)
    : ProgressBar(setting, service, taskId)
    where TCustomStatus : ProgressBarStatus
{
    private TCustomStatus? _customStatus;

    /// <summary>
    /// Please use <see cref="CustomStatus"/>
    /// </summary>
    public override ProgressBarStatus Status
    {
        get => CustomStatus;
        protected set
        {
            if (value is TCustomStatus customStatus) CustomStatus = customStatus;
            throw new InvalidOperationException(
                $"Can not set origin status when using custom status property in {GetType().GetCleanFullName()}!");
        }
    }

    public TCustomStatus CustomStatus
    {
        get => _customStatus ?? throw new InvalidOperationException($"未能成功获取进度状态，请检查是否未调用{nameof(InitProgressBarStatus)}");
        protected set => _customStatus = value;
    }


    public abstract TCustomStatus CreateDefaultCustomStatus();

    public override void InitProgressBarStatus(object? distributedStatus = null)
    {
        CustomStatus = distributedStatus switch
        {
            null => CreateDefaultCustomStatus(),
            TCustomStatus status => status,
            _ => throw new InvalidOperationException($"{distributedStatus.GetType().GetCleanFullName()} 不是有效的 {typeof(TCustomStatus).Name} 类型，无法初始化进度条状态")
        };
    }
}



/// <summary>
/// Progress bar class, used to track and manage the execution progress of tasks
/// </summary>
/// <remarks>
/// Progress bar class, used to track and manage the execution progress of tasks
/// </remarks>
/// <param name="setting"></param>
/// <param name="service">Progress bar service interface</param>
/// <param name="taskId">task unique identifier</param>
public class ProgressBar(ProgressBarSetting setting, IMoProgressBarService service, string taskId)
{
    private bool _isCancelled;
    private CancellationToken? _cancellationToken;

    /// <summary>
    /// task unique identifier
    /// </summary>
    public string TaskId => Status.Id;

    public ProgressBarSetting Setting { get; } = setting;

    private ProgressBarStatus? _status;
    /// <summary>
    /// Current progress status
    /// </summary>
    public virtual ProgressBarStatus Status
    {
        get => _status ?? throw new InvalidOperationException($"未能成功获取进度状态，请检查是否未调用{nameof(InitProgressBarStatus)}");
        protected set => _status = value;
    }

    /// <summary>
    /// Has the task been completed?
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Whether the task has been canceled
    /// </summary>
    public bool IsCancelled => _isCancelled || (_cancellationToken?.IsCancellationRequested ?? false);

    /// <summary>
    /// Get the cancellation token associated with this progress bar
    /// </summary>
    public CancellationToken CancellationToken => _cancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Progress bar service interface, used to save progress status
    /// </summary>
    protected IMoProgressBarService Service { get; } = service;

    /// <summary>
    /// Progress bar status update event
    /// </summary>
    public event EventHandler<ProgressBarEventArgs>? StatusUpdated;

    /// <summary>
    /// Progress bar cancellation event
    /// </summary>
    public event EventHandler<ProgressBarCancelledEventArgs>? Cancelled;

    /// <summary>
    /// progress bar completion event
    /// </summary>
    public event EventHandler<ProgressBarEventArgs>? Completed;

    /// <summary>
    /// Initialize progress bar state
    /// </summary>
    /// <param name="distributedStatus"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public virtual void InitProgressBarStatus(object? distributedStatus = null)
    {
        _status = distributedStatus switch
        {
            null => new ProgressBarStatus(Setting.TotalSteps, taskId),
            ProgressBarStatus status => status,
            _ => throw new InvalidOperationException($"{distributedStatus.GetType().GetCleanFullName()} 不是有效的 {nameof(ProgressBarStatus)} 类型，无法初始化进度条状态")
        };
    }

    #region 分布式操作

    /// <summary>
    /// Distributed imprint, indicating the service where the current distributed progress bar is located
    /// </summary>
    public string? DistributedStamp { get; set; }


    /// <summary>
    /// Whether the distributed progress bar is currently controllable (indicating that the current service is changing the status of the progress bar)
    /// </summary>
    public bool IsCurrentTurn => Setting.DistributedStamp == DistributedStamp;

    #endregion

    /// <summary>
    /// Set cancellation token
    /// </summary>
    /// <param name="cancellationToken">cancel token</param>
    internal void SetCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        
        // Register cancellation event listener
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                if (IsCompleted || _isCancelled) return;
                
                _isCancelled = true;
                OnCancelled(new ProgressBarCancelledEventArgs(this, "Task was cancelled externally."));
            });
        }
    }

    /// <summary>
    /// Trigger status update event
    /// </summary>
    /// <param name="e">event parameters</param>
    protected virtual void OnStatusUpdated(ProgressBarEventArgs e)
    {
        StatusUpdated?.Invoke(this, e);
    }

    /// <summary>
    /// trigger cancel event
    /// </summary>
    /// <param name="e">Cancel event parameters</param>
    protected virtual void OnCancelled(ProgressBarCancelledEventArgs e)
    {
        Cancelled?.Invoke(this, e);
    }

    /// <summary>
    /// trigger completion event
    /// </summary>
    /// <param name="e">event parameters</param>
    protected virtual void OnCompleted(ProgressBarEventArgs e)
    {
        Completed?.Invoke(this, e);
    }

    /// <summary>
    /// Save the current progress status to the storage service
    /// </summary>
    /// <param name="saveInstantly">Whether to save immediately, default false. If the progress bar has auto-update settings and this parameter is false, it will not be saved immediately.</param>
    /// <returns>Asynchronous tasks</returns>
    public virtual async ValueTask SaveStatus(bool saveInstantly = false)
    {
        if (IsCompleted || IsCancelled) return;
        
        Status.LastUpdated = DateTime.Now;
        await Service.SaveProgressBarStateAsync(this, saveInstantly);
        
        // Trigger status update event
        OnStatusUpdated(new ProgressBarEventArgs(this));
    }

    /// <summary>
    /// Update task progress status
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
        if (!string.IsNullOrEmpty(statusMessage))
        {
            Status.CurrentStatus = statusMessage;
        }
        if (!string.IsNullOrEmpty(phase))
        {
            Status.Phase = phase;
        }
        await SaveStatus();
    }

    /// <summary>
    /// Incremental progress
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
    /// Update current stage
    /// </summary>
    /// <param name="phase">Stage name</param>
    /// <param name="statusMessage">Status message (optional)</param>
    /// <returns>Asynchronous tasks</returns>
    public virtual async Task UpdatePhaseAsync(string phase, string? statusMessage = null)
    {
        ThrowIfCancellationRequested();
        if (IsCompleted || IsCancelled) return;
        
        Status.Phase = phase;
        if (!string.IsNullOrEmpty(statusMessage))
        {
            Status.CurrentStatus = statusMessage;
        }
        await SaveStatus();
    }

    /// <summary>
    /// Complete the task and save the final status
    /// </summary>
    /// <returns>Asynchronous tasks</returns>
    public virtual async Task CompleteTaskAsync(string? statusMessage = null, string? phase = null)
    {
        if (IsCompleted || IsCancelled) return;
        if (!string.IsNullOrEmpty(statusMessage))
        {
            Status.CurrentStatus = statusMessage;
        }
        if (!string.IsNullOrEmpty(phase))
        {
            Status.Phase = phase;
        }
        IsCompleted = true;
        Status.CurrentStep = Status.TotalSteps;
        await Service.FinishProgressBarAsync(this);
        
        // trigger completion event
        OnCompleted(new ProgressBarEventArgs(this));
    }

    /// <summary>
    /// Cancel task
    /// </summary>
    /// <param name="reason">Reason for cancellation</param>
    /// <returns>Asynchronous tasks</returns>
    public virtual async Task CancelTaskAsync(string? reason = null)
    {
        if (IsCompleted || _isCancelled) return;
        
        _isCancelled = true;
        
        // Set the cancellation mark and cancellation reason in the status
        Status.IsCancelled = true;
        if (!string.IsNullOrEmpty(reason))
        {
            Status.CancelReason = reason;
        }
        
        await Service.CancelProgressBarAsync(this, reason);
        
        // trigger cancel event
        OnCancelled(new ProgressBarCancelledEventArgs(this, reason));
    }

    /// <summary>
    /// Check if it has been canceled and throw OperationCancelledException if it has been canceled
    /// </summary>
    public virtual void ThrowIfCancellationRequested()
    {
        if (IsCancelled)
        {
            throw new InvalidOperationException($"Progress bar task '{TaskId}' has been cancelled.");
        }
        
        _cancellationToken?.ThrowIfCancellationRequested();
    }
}