using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.UI.Models;

/// <summary>
/// 轻量级 Job 实例投影，仅包含统计分析所需字段
/// 不包含 StateHistory 和 JobArgs 等大文本字段，用于减少数据传输
/// </summary>
public record JobInstanceStatistics
{
    /// <summary>
    /// 实例唯一标识符
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// 作业键
    /// </summary>
    public string JobKey { get; init; } = string.Empty;

    /// <summary>
    /// 执行状态
    /// </summary>
    public JobState State { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 开始执行时间
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryAttempt { get; init; }
}
