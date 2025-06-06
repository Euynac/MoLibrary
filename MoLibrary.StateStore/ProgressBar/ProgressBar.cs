namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 进度条类，用于跟踪和管理任务的执行进度
/// </summary>
/// <param name="service">进度条服务接口</param>
/// <param name="taskId">任务唯一标识符</param>
public class ProgressBar(ProgressBarSetting setting, IMoProgressBarService service, string taskId)
{
    private readonly ProgressBarStatus _progressBarStatus = new(setting.TotalSteps);

    /// <summary>
    /// 任务唯一标识符
    /// </summary>
    public string TaskId { get; } = taskId;

    public ProgressBarSetting Setting { get; } = setting;

    /// <summary>
    /// 进度条服务接口，用于保存进度状态
    /// </summary>
    protected IMoProgressBarService Service { get; } = service;

    /// <summary>
    /// 进度条状态更新事件
    /// </summary>
    public event EventHandler<ProgressBarEventArgs>? StatusUpdated;

    /// <summary>
    /// 触发状态更新事件
    /// </summary>
    /// <param name="e">事件参数</param>
    protected virtual void OnStatusUpdated(ProgressBarEventArgs e)
    {
        StatusUpdated?.Invoke(this, e);
    }

    /// <summary>
    /// 保存当前进度状态到存储服务
    /// </summary>
    /// <returns>异步任务</returns>
    public virtual async ValueTask SaveStatus()
    {
        _progressBarStatus.LastUpdated = DateTime.Now;
        await Service.SaveProgressBarStateAsync(this);
        
        // 触发状态更新事件
        OnStatusUpdated(new ProgressBarEventArgs(this));
    }

    /// <summary>
    /// 更新任务进度状态
    /// </summary>
    /// <param name="currentStep">当前步数，会确保不小于0</param>
    /// <returns>异步任务</returns>
    public virtual async Task UpdateStatusAsync(int currentStep)
    {
        _progressBarStatus.CurrentStep = Math.Max(0, currentStep);
        await SaveStatus();
    }

    /// <summary>
    /// 完成任务，保存最终状态
    /// </summary>
    /// <returns>异步任务</returns>
    public virtual async Task CompleteTaskAsync()
    {
        await Service.FinishProgressBarAsync(this);
    }
}