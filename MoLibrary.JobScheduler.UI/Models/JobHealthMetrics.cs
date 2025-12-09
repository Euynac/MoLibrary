namespace MoLibrary.JobScheduler.UI.Models;

using MoLibrary.JobScheduler.Models;

/// <summary>
/// 作业健康指标
/// </summary>
public class JobHealthMetrics
{
    public string JobKey { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }

    // State count properties
    public int SucceededCount { get; set; }
    public int SkippedCount { get; set; }
    public int CancelledCount { get; set; }
    public int ProcessingCount { get; set; }
    public int FailedCount { get; set; }
    public int TerminatedCount { get; set; }
    public int EnqueuedCount { get; set; }
    public int ScheduledCount { get; set; }

    /// <summary>
    /// 健康度 - 衡量作业调度的有效性
    /// 公式: (总执行次数 - 跳过 - 失败 - 终止) / 总执行次数 * 100
    /// </summary>
    public double HealthScore => TotalExecutions > 0
        ? ((TotalExecutions - SkippedCount - FailedCount - TerminatedCount) / (double)TotalExecutions) * 100
        : 100;

    /// <summary>
    /// 执行失败率 - 衡量作业代码的可靠性
    /// 公式: (失败 + 终止) / 实际完成数量 * 100
    /// 实际完成数量 = 总执行次数 - 跳过 - 取消 - 运行中 - 已入队 - 已调度
    /// </summary>
    public double ExecutionFailureRate
    {
        get
        {
            var completedExecutions = TotalExecutions - SkippedCount - CancelledCount - ProcessingCount - EnqueuedCount - ScheduledCount;
            if (completedExecutions <= 0) return 0;
            return ((FailedCount + TerminatedCount) / (double)completedExecutions) * 100;
        }
    }

    public List<JobInstance> RecentFailures { get; set; } = new();
    public DateTime MetricsStartTime { get; set; }
    public DateTime MetricsEndTime { get; set; }
}
