using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.UI.Models;

/// <summary>
/// 带有上次执行信息的作业定义
/// </summary>
public class JobDefinitionWithLastExecution
{
    /// <summary>
    /// 作业定义
    /// </summary>
    public required JobDefinition Definition { get; set; }

    /// <summary>
    /// 上次执行的实例（如果有）
    /// </summary>
    public JobInstance? LastExecution { get; set; }

    /// <summary>
    /// 下次执行时间（仅针对 RecurringJob）
    /// </summary>
    public DateTime? NextExecutionTime { get; set; }
}
