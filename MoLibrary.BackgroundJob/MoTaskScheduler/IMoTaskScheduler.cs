namespace MoLibrary.BackgroundJob.MoTaskScheduler;

public interface IMoTaskScheduler
{
    /// <summary>
    ///     添加定时任务
    /// </summary>
    /// <param name="expression">Crontab 表达式</param>
    /// <param name="action">要执行的任务</param>
    /// <param name="startAt">任务开始时间</param>
    /// <param name="endAt">任务结束时间</param>
    /// <returns>任务 ID</returns>
    int AddTask(string expression, Action action, DateTime? startAt = null, DateTime? endAt = null);

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

public class ScheduledTask
{
    public Action Action { get; set; }
    public DateTime? EndAt { get; set; }
    public string Expression { get; set; }
    public int Id { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime StartAt { get; set; }
    public int TotalExecutedTimes { get; set; }
}