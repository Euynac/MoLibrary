namespace MoLibrary.StateStore.ProgressBar;

public class ProgressBarStatus(int totalSteps)
{
    /// <summary>
    /// 总步数
    /// </summary>
    public int TotalSteps { get; } = totalSteps;

    /// <summary>
    /// 当前进度步数
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// 已经过的时间
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.Now - StartTime;

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
    /// 进度百分比，计算当前步数占总步数的百分比，保留两位小数
    /// </summary>
    public virtual double Percentage => TotalSteps > 0 ?
        Math.Round((double) CurrentStep / TotalSteps * 100, 2) : 0;

    /// <summary>
    /// 预估剩余时间，基于当前进度计算剩余完成时间
    /// </summary>
    public TimeSpan EstimatedRemaining => CalculateRemainingTime();
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
}