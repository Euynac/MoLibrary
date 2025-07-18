namespace MoLibrary.FrameworkUI.UITimekeeper.Models;

/// <summary>
/// Timekeeper统计信息响应模型
/// </summary>
public class TimekeeperStatisticsResponse
{
    /// <summary>
    /// 计时器名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 执行次数
    /// </summary>
    public int Times { get; set; }

    /// <summary>
    /// 平均执行时间
    /// </summary>
    public string Average { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public string CreateAt { get; set; } = string.Empty;

    /// <summary>
    /// 每分钟执行次数
    /// </summary>
    public string TimesEveryMinutes { get; set; } = string.Empty;

    /// <summary>
    /// 平均内存使用量
    /// </summary>
    public string? AverageMemory { get; set; }

    /// <summary>
    /// 最后一次内存使用量
    /// </summary>
    public string? LastMemory { get; set; }

    /// <summary>
    /// 最后一次执行时长
    /// </summary>
    public string LastDuration { get; set; } = string.Empty;

    /// <summary>
    /// 最后执行时间
    /// </summary>
    public string? LastExecutedTime { get; set; }
}

/// <summary>
/// 正在运行的Timekeeper信息响应模型
/// </summary>
public class RunningTimekeeperResponse
{
    /// <summary>
    /// 计时器名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 内容描述
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// 当前经过时间
    /// </summary>
    public string CurrentElapsed { get; set; } = string.Empty;

    /// <summary>
    /// 运行时长
    /// </summary>
    public string RunningDuration { get; set; } = string.Empty;
} 