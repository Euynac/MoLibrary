namespace MoLibrary.BackgroundJob.MoTaskScheduler;

public interface IMoTaskScheduler
{
    /// <summary>
    ///     添加定时任务
    /// </summary>
    /// <param name="expression">Crontab 表达式</param>
    /// <param name="task"></param>
    /// <param name="startAt">任务开始时间</param>
    /// <param name="endAt">任务结束时间</param>
    /// <param name="skipWhenPreviousIsRunning">当前一个任务实例还在运行时是否跳过本次执行</param>
    /// <returns>任务 ID</returns>
    int AddTask(string expression, Func<Task> task, DateTime? startAt = null, DateTime? endAt = null,
        bool skipWhenPreviousIsRunning = false);

    /// <summary>
    ///     删除定时任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <returns>是否删除成功</returns>
    bool DeleteTask(int taskId);

    /// <summary>
    ///     禁用定时任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <returns>是否禁用成功</returns>
    bool DisableTask(int taskId);

    /// <summary>
    ///     启用定时任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <returns>是否启用成功</returns>
    bool EnableTask(int taskId);

    /// <summary>
    ///     获取所有定时任务
    /// </summary>
    /// <returns>定时任务列表</returns>
    IEnumerable<ScheduledTask> GetAllTasks();
}

/// <summary>
/// 定时任务模型
/// </summary>
public class ScheduledTask
{
    /// <summary>
    /// 要执行的任务动作
    /// </summary>
    public required Func<Task> Task { get; set; }
    
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
}