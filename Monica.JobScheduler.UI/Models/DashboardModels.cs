namespace Monica.JobScheduler.UI.Models;

using Monica.JobScheduler.Models;

/// <summary>
/// 系统健康状态
/// </summary>
public enum SystemHealthStatus
{
    /// <summary>
    /// 健康 - 所有组件正常运行
    /// </summary>
    Healthy,

    /// <summary>
    /// 降级 - 部分组件异常但系统仍可运行
    /// </summary>
    Degraded,

    /// <summary>
    /// 异常 - 关键组件故障
    /// </summary>
    Unhealthy
}

/// <summary>
/// 仪表盘总览数据
/// </summary>
public class DashboardSummary
{
    /// <summary>
    /// 任务定义总数
    /// </summary>
    public int TotalJobs { get; set; }

    /// <summary>
    /// 周期任务数量
    /// </summary>
    public int RecurringJobCount { get; set; }

    /// <summary>
    /// 触发任务数量
    /// </summary>
    public int TriggeredJobCount { get; set; }

    /// <summary>
    /// 已禁用的任务数量
    /// </summary>
    public int DisabledJobCount { get; set; }

    /// <summary>
    /// 当前正在运行的实例数
    /// </summary>
    public int RunningNow { get; set; }

    /// <summary>
    /// 成功率（基于指定时间窗口）
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 每小时吞吐量
    /// </summary>
    public int ThroughputPerHour { get; set; }

    /// <summary>
    /// 各状态实例分布
    /// </summary>
    public Dictionary<JobState, int> StateDistribution { get; set; } = new();

    /// <summary>
    /// 系统健康状态
    /// </summary>
    public SystemHealthStatus HealthStatus { get; set; }

    /// <summary>
    /// 健康状态描述
    /// </summary>
    public string? HealthMessage { get; set; }

    /// <summary>
    /// 统计时间窗口起始时间
    /// </summary>
    public DateTime MetricsStartTime { get; set; }

    /// <summary>
    /// 统计时间窗口结束时间
    /// </summary>
    public DateTime MetricsEndTime { get; set; }
}

/// <summary>
/// 最近活动记录
/// </summary>
public class RecentActivity
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
    /// 最终状态
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// 活动时间（完成时间或创建时间）
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 执行耗时（仅对已完成的任务）
    /// </summary>
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// 问题任务集合
/// </summary>
public class ProblemJobs
{
    /// <summary>
    /// 连续失败的任务
    /// </summary>
    public List<ConsecutiveFailureJob> ConsecutiveFailures { get; set; } = [];

    /// <summary>
    /// 长时间运行的任务
    /// </summary>
    public List<LongRunningJob> LongRunning { get; set; } = [];

    /// <summary>
    /// 跳过率过高的任务
    /// </summary>
    public List<HighSkipRateJob> HighSkipRate { get; set; } = [];

    /// <summary>
    /// 是否有任何问题任务
    /// </summary>
    public bool HasProblems => ConsecutiveFailures.Count > 0 || LongRunning.Count > 0 || HighSkipRate.Count > 0;

    /// <summary>
    /// 问题任务总数
    /// </summary>
    public int TotalProblemCount => ConsecutiveFailures.Count + LongRunning.Count + HighSkipRate.Count;
}

/// <summary>
/// 连续失败的任务
/// </summary>
public class ConsecutiveFailureJob
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
    /// 连续失败次数
    /// </summary>
    public int ConsecutiveFailureCount { get; set; }

    /// <summary>
    /// 最后一次失败时间
    /// </summary>
    public DateTime LastFailureTime { get; set; }

    /// <summary>
    /// 最后一次失败的实例ID
    /// </summary>
    public string? LastFailureInstanceId { get; set; }
}

/// <summary>
/// 长时间运行的任务
/// </summary>
public class LongRunningJob
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
    /// 已运行时间
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// 最大执行超时时间
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 超时百分比 (已运行/最大超时 * 100)
    /// </summary>
    public double TimeoutPercentage => MaxExecutionTimeout.TotalSeconds > 0
        ? (ElapsedTime.TotalSeconds / MaxExecutionTimeout.TotalSeconds) * 100
        : 0;
}

/// <summary>
/// 跳过率过高的任务
/// </summary>
public class HighSkipRateJob
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
    /// 跳过次数
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 跳过率 (0-100)
    /// </summary>
    public double SkipRate => TotalCount > 0 ? ((double)SkippedCount / TotalCount) * 100 : 0;

    /// <summary>
    /// 最大并发数配置
    /// </summary>
    public int MaxConcurrency { get; set; }
}

/// <summary>
/// Pre-loaded data context for dashboard operations.
/// All data is loaded once and passed to service methods for in-memory processing.
/// </summary>
public class DashboardDataContext
{
    /// <summary>
    /// All job definitions (from cache)
    /// </summary>
    public required IReadOnlyList<JobDefinition> Definitions { get; init; }

    /// <summary>
    /// Job name lookup map
    /// </summary>
    public required IReadOnlyDictionary<string, string> JobNameMap { get; init; }

    /// <summary>
    /// Job config lookup map
    /// </summary>
    public required IReadOnlyDictionary<string, JobDefinition> JobConfigMap { get; init; }

    /// <summary>
    /// State distribution statistics (from optimized GROUP BY)
    /// </summary>
    public required IReadOnlyDictionary<JobState, int> StateDistribution { get; init; }

    /// <summary>
    /// All instances in metrics window (lightweight projection)
    /// </summary>
    public required IReadOnlyList<InstanceProjection> AllInstances { get; init; }

    /// <summary>
    /// Metrics time window start
    /// </summary>
    public required DateTime MetricsStartTime { get; init; }

    /// <summary>
    /// Metrics time window end
    /// </summary>
    public required DateTime MetricsEndTime { get; init; }
}

/// <summary>
/// Lightweight projection for instance data - only fields needed for analysis
/// </summary>
public record InstanceProjection(
    string InstanceId,
    string JobKey,
    JobState State,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);
