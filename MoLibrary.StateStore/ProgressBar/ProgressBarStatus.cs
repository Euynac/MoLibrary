namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 进度条状态更新事件参数
/// </summary>
public class ProgressBarStatusEventArgs(ProgressBarStatus status) : EventArgs
{
    public ProgressBarStatus Status { get; } = status;
}

/// <summary>
/// 进度条状态类，用于跟踪和管理任务的执行进度
/// </summary>
/// <param name="service">进度条服务接口</param>
/// <param name="taskId">任务唯一标识符</param>
public class ProgressBarStatus(ProgressBarSetting setting, IProgressBarService service, string taskId)
{
    public ProgressBarSetting Setting { get; } = setting;

    /// <summary>
    /// 进度条服务接口，用于保存进度状态
    /// </summary>
    protected IProgressBarService Service { get; } = service;
    
    /// <summary>
    /// 任务唯一标识符
    /// </summary>
    public string TaskId { get; } = taskId;
    
    /// <summary>
    /// 当前进度步数
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// 总步数
    /// </summary>
    public int TotalSteps => Setting.TotalSteps;

    /// <summary>
    /// 进度百分比，计算当前步数占总步数的百分比，保留两位小数
    /// </summary>
    public virtual double Percentage => TotalSteps > 0 ?
        Math.Round((double) CurrentStep / TotalSteps * 100, 2) : 0;

    /// <summary>
    /// 已经过的时间
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.Now - StartTime;
    
    /// <summary>
    /// 预估剩余时间，基于当前进度计算剩余完成时间
    /// </summary>
    public TimeSpan EstimatedRemaining => CalculateRemainingTime();
    
    /// <summary>
    /// 当前状态描述
    /// </summary>
    public string? CurrentStatus { get; set; }
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 任务开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 进度条状态更新事件
    /// </summary>
    public event EventHandler<ProgressBarStatusEventArgs>? StatusUpdated;

    /// <summary>
    /// 触发状态更新事件
    /// </summary>
    /// <param name="e">事件参数</param>
    protected virtual void OnStatusUpdated(ProgressBarStatusEventArgs e)
    {
        StatusUpdated?.Invoke(this, e);
    }

    /// <summary>
    /// 计算预估剩余完成时间
    /// </summary>
    /// <returns>预估剩余时间，如果当前步数小于等于0则返回最大时间值</returns>
    public virtual TimeSpan CalculateRemainingTime()
    {
        if (CurrentStep <= 0) return TimeSpan.MaxValue;

        var elapsedPerStep = ElapsedTime.TotalMilliseconds / CurrentStep;
        var remainingSteps = TotalSteps - CurrentStep;
        return TimeSpan.FromMilliseconds(elapsedPerStep * remainingSteps);
    }

    /// <summary>
    /// 保存当前进度状态到存储服务
    /// </summary>
    /// <returns>异步任务</returns>
    public virtual async ValueTask SaveStatus()
    {
        LastUpdated = DateTime.Now;
        await Service.SaveProgressBarStateAsync(this);
        
        // 触发状态更新事件
        OnStatusUpdated(new ProgressBarStatusEventArgs(this));
    }

    /// <summary>
    /// 更新任务进度状态
    /// </summary>
    /// <param name="currentStep">当前步数，会确保不小于0</param>
    /// <returns>异步任务</returns>
    public virtual async Task UpdateStatusAsync(int currentStep)
    {
        CurrentStep = Math.Max(0, currentStep);
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