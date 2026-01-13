namespace MoLibrary.JobScheduler.UI.Models;

using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.Models;

/// <summary>
/// 实时执行信息
/// </summary>
public class LiveExecution
{
    /// <summary>
    /// 实例ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// 任务Key
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// 当前状态
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// ���始执行时间（仅Processing状态）
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 已运行时间（仅Processing状态）
    /// </summary>
    public TimeSpan? ElapsedTime { get; set; }

    /// <summary>
    /// 执行该任务的Worker客户端ID
    /// </summary>
    public string? WorkerClientId { get; set; }

    /// <summary>
    /// 最大执行超时时间
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; set; }

    /// <summary>
    /// 超时进度百分比 (0-100)
    /// </summary>
    public double TimeoutProgress => ElapsedTime.HasValue && MaxExecutionTimeout.TotalSeconds > 0
        ? Math.Min(100, (ElapsedTime.Value.TotalSeconds / MaxExecutionTimeout.TotalSeconds) * 100)
        : 0;
}

/// <summary>
/// 队列状态
/// </summary>
public class QueueStatus
{
    /// <summary>
    /// 已入队等待执行的实例数
    /// </summary>
    public int EnqueuedCount { get; set; }

    /// <summary>
    /// 已调度等待触发的实例数
    /// </summary>
    public int ScheduledCount { get; set; }

    /// <summary>
    /// 正在处理的实例数
    /// </summary>
    public int ProcessingCount { get; set; }

    /// <summary>
    /// 等待中的总数 (Enqueued + Scheduled)
    /// </summary>
    public int PendingCount => EnqueuedCount + ScheduledCount;

    /// <summary>
    /// 活跃实例总数 (所有非终态)
    /// </summary>
    public int ActiveCount => EnqueuedCount + ScheduledCount + ProcessingCount;
}

/// <summary>
/// 并发使用情况
/// </summary>
public class ConcurrencyUsage
{
    /// <summary>
    /// 任务Key
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// 当前运行中的实例数
    /// </summary>
    public int CurrentRunning { get; set; }

    /// <summary>
    /// 等待中的实例数（已预留槽位）
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// 当前占用的总数 (Running + Pending)
    /// </summary>
    public int CurrentExecuting => CurrentRunning + PendingCount;

    /// <summary>
    /// 最大并发数
    /// </summary>
    public int MaxConcurrency { get; set; }

    /// <summary>
    /// 利用率百分比 (0-100)
    /// </summary>
    public double UtilizationPercent => MaxConcurrency > 0
        ? Math.Min(100, ((double)CurrentExecuting / MaxConcurrency) * 100)
        : 0;

    /// <summary>
    /// 是否达到最大并发
    /// </summary>
    public bool IsAtCapacity => CurrentExecuting >= MaxConcurrency;
}

/// <summary>
/// 监控页面的刷新间隔选项
/// </summary>
public enum RefreshInterval
{
    /// <summary>
    /// 3秒
    /// </summary>
    ThreeSeconds = 3000,

    /// <summary>
    /// 5秒
    /// </summary>
    FiveSeconds = 5000,

    /// <summary>
    /// 10秒
    /// </summary>
    TenSeconds = 10000,

    /// <summary>
    /// 30秒
    /// </summary>
    ThirtySeconds = 30000
}

/// <summary>
/// 监控页面状态
/// </summary>
public class MonitorState
{
    /// <summary>
    /// 实时执行列表
    /// </summary>
    public List<LiveExecution> LiveExecutions { get; set; } = [];

    /// <summary>
    /// 队列状态
    /// </summary>
    public QueueStatus QueueStatus { get; set; } = new();

    /// <summary>
    /// 并发使用情况列表（仅显示有活跃执行的任务）
    /// </summary>
    public List<ConcurrencyUsage> ConcurrencyUsages { get; set; } = [];

    /// <summary>
    /// 上次刷新时间
    /// </summary>
    public DateTime LastRefreshTime { get; set; }
}

#region Concurrency Monitor Models

/// <summary>
/// 带 JobName 的并发状态（用于 UI 显示）
/// </summary>
public class ConcurrencyStatusWithName
{
    /// <summary>
    /// 任务名称
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// 原始统计数据
    /// </summary>
    public required JobExecutionStatistic Statistic { get; set; }

    /// <summary>
    /// 利用率百分比
    /// </summary>
    public double UtilizationPercent => Statistic.MaxConcurrency > 0
        ? Math.Min(100, (double)Statistic.CurrentExecutingCount / Statistic.MaxConcurrency * 100)
        : 0;

    /// <summary>
    /// 是否达到最大并发
    /// </summary>
    public bool IsAtCapacity => Statistic.CurrentExecutingCount >= Statistic.MaxConcurrency;
}

/// <summary>
/// 并发监控状态（包含详细信息和一致性检测）
/// </summary>
public class ConcurrencyMonitorState
{
    /// <summary>
    /// 详细的并发状态列表（带名称）
    /// </summary>
    public List<ConcurrencyStatusWithName> Details { get; set; } = [];

    /// <summary>
    /// 一致性检测结果
    /// </summary>
    public required ConsistencyCheckResult Consistency { get; set; }

    /// <summary>
    /// 上次刷新时间
    /// </summary>
    public DateTime LastRefreshTime { get; set; }
}

#endregion
