using MoLibrary.StateStore.CancellationManager;

namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 进度条类，用于跟踪和管理任务的执行进度
/// </summary>
/// <param name="service">进度条服务接口</param>
/// <param name="taskId">任务唯一标识符</param>
public class ProgressBar(ProgressBarSetting setting, IMoProgressBarService service, string taskId)
{
    private bool _isCancelled = false;
    private CancellationToken? _cancellationToken;

    /// <summary>
    /// 任务唯一标识符
    /// </summary>
    public string TaskId { get; } = taskId;

    public ProgressBarSetting Setting { get; } = setting;

    /// <summary>
    /// 当前进度状态
    /// </summary>
    public ProgressBarStatus Status { get; } = new(setting.TotalSteps);

    /// <summary>
    /// 任务是否已完成
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// 任务是否已取消
    /// </summary>
    public bool IsCancelled => _isCancelled || (_cancellationToken?.IsCancellationRequested ?? false);

    /// <summary>
    /// 获取与此进度条关联的取消令牌
    /// </summary>
    public CancellationToken CancellationToken => _cancellationToken ?? CancellationToken.None;

    /// <summary>
    /// 进度条服务接口，用于保存进度状态
    /// </summary>
    protected IMoProgressBarService Service { get; } = service;

    /// <summary>
    /// 进度条状态更新事件
    /// </summary>
    public event EventHandler<ProgressBarEventArgs>? StatusUpdated;

    /// <summary>
    /// 进度条取消事件
    /// </summary>
    public event EventHandler<ProgressBarCancelledEventArgs>? Cancelled;

    /// <summary>
    /// 进度条完成事件
    /// </summary>
    public event EventHandler<ProgressBarEventArgs>? Completed;

    /// <summary>
    /// 设置取消令牌
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    internal void SetCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        
        // 注册取消事件监听
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
    /// 触发状态更新事件
    /// </summary>
    /// <param name="e">事件参数</param>
    protected virtual void OnStatusUpdated(ProgressBarEventArgs e)
    {
        StatusUpdated?.Invoke(this, e);
    }

    /// <summary>
    /// 触发取消事件
    /// </summary>
    /// <param name="e">取消事件参数</param>
    protected virtual void OnCancelled(ProgressBarCancelledEventArgs e)
    {
        Cancelled?.Invoke(this, e);
    }

    /// <summary>
    /// 触发完成事件
    /// </summary>
    /// <param name="e">事件参数</param>
    protected virtual void OnCompleted(ProgressBarEventArgs e)
    {
        Completed?.Invoke(this, e);
    }

    /// <summary>
    /// 保存当前进度状态到存储服务
    /// </summary>
    /// <param name="saveInstantly">是否立即保存，默认false。如果进度条有自动更新设置且此参数为false，则不会立即保存</param>
    /// <returns>异步任务</returns>
    public virtual async ValueTask SaveStatus(bool saveInstantly = false)
    {
        if (IsCompleted || IsCancelled) return;
        
        Status.LastUpdated = DateTime.Now;
        await Service.SaveProgressBarStateAsync(this, saveInstantly);
        
        // 触发状态更新事件
        OnStatusUpdated(new ProgressBarEventArgs(this));
    }

    /// <summary>
    /// 更新任务进度状态
    /// </summary>
    /// <param name="currentStep">当前步数，会确保不小于0</param>
    /// <param name="statusMessage">状态消息</param>
    /// <returns>异步任务</returns>
    public virtual async Task UpdateStatusAsync(int currentStep, string? statusMessage = null)
    {
        ThrowIfCancellationRequested();
        if (IsCompleted || IsCancelled) return;
        
        Status.CurrentStep = Math.Max(0, currentStep);
        if (!string.IsNullOrEmpty(statusMessage))
        {
            Status.CurrentStatus = statusMessage;
        }
        await SaveStatus();
    }

    /// <summary>
    /// 递增进度
    /// </summary>
    /// <param name="increment">递增步数，默认为1</param>
    /// <param name="statusMessage">状态消息</param>
    /// <returns>异步任务</returns>
    public virtual async Task IncrementAsync(int increment = 1, string? statusMessage = null)
    {
        await UpdateStatusAsync(Status.CurrentStep + increment, statusMessage);
    }

    /// <summary>
    /// 完成任务，保存最终状态
    /// </summary>
    /// <returns>异步任务</returns>
    public virtual async Task CompleteTaskAsync()
    {
        if (IsCompleted || IsCancelled) return;
        
        IsCompleted = true;
        Status.CurrentStep = Status.TotalSteps;
        await Service.FinishProgressBarAsync(this);
        
        // 触发完成事件
        OnCompleted(new ProgressBarEventArgs(this));
    }

    /// <summary>
    /// 取消任务
    /// </summary>
    /// <param name="reason">取消原因</param>
    /// <returns>异步任务</returns>
    public virtual async Task CancelTaskAsync(string? reason = null)
    {
        if (IsCompleted || _isCancelled) return;
        
        _isCancelled = true;
        await Service.CancelProgressBarAsync(this, reason);
        
        // 触发取消事件
        OnCancelled(new ProgressBarCancelledEventArgs(this, reason));
    }

    /// <summary>
    /// 检查是否已取消，如果已取消则抛出OperationCancelledException
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