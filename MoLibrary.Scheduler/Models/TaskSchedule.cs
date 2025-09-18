namespace MoLibrary.Scheduler.Models;
/// <summary>
/// 定时任务模型
/// </summary>
public class ScheduledTask
{
    /// <summary>
    /// 任务名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 任务结束时间，如果为 null 表示任务永不结束
    /// </summary>
    public DateTime? EndAt { get; set; }
    
    /// <summary>
    /// Cron 表达式，用于定义任务的执行时间规则
    /// </summary>
    public required string Expression { get; set; }
    
    /// <summary>
    /// 任务的唯一标识符
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 任务是否启用，默认为 true
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 任务开始时间，任务只会在此时间之后执行
    /// </summary>
    public DateTime StartAt { get; set; }
    
    /// <summary>
    /// 任务已执行的总次数
    /// </summary>
    public int TotalExecutedTimes { get; set; }
    
    /// <summary>
    /// 当前一个任务实例还在运行时是否跳过本次执行
    /// 如果为 true，当任务正在执行时，下一次调度将被跳过
    /// 如果为 false，即使任务正在执行，下一次调度也会开始新的执行
    /// </summary>
    public bool SkipWhenPreviousIsRunning { get; set; }
    
    /// <summary>
    /// 任务当前是否正在运行（内部使用）
    /// </summary>
    internal bool IsRunning { get; set; }

    /// <summary>
    /// 获取任务名称
    /// </summary>
    /// <returns>任务名称</returns>
    public string GetTaskName()
    {
        return Name ?? Id.ToString();
    }

    public override string ToString()
    {
        return $"Task {Name ?? Id.ToString()}";
    }
}