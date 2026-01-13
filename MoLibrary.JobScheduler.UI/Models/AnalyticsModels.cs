namespace MoLibrary.JobScheduler.UI.Models;

/// <summary>
/// 时间粒度
/// </summary>
public enum TimeGranularity
{
    /// <summary>
    /// 按小时聚合
    /// </summary>
    Hourly,

    /// <summary>
    /// 按天聚合
    /// </summary>
    Daily,

    /// <summary>
    /// 按周聚合
    /// </summary>
    Weekly
}

/// <summary>
/// 执行趋势数据点
/// </summary>
public class ExecutionTrendPoint
{
    /// <summary>
    /// 时间点（聚合周期的起始时间）
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 成功执行数量
    /// </summary>
    public int SucceededCount { get; set; }

    /// <summary>
    /// 失败执行数量（Failed + Terminated）
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 跳过数量
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 取消数量
    /// </summary>
    public int CancelledCount { get; set; }

    /// <summary>
    /// 总执行数量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 该时间段的成功率 (0-100)
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? ((double)SucceededCount / TotalCount) * 100 : 100;
}

/// <summary>
/// 执行耗时百分位数
/// </summary>
public class DurationPercentiles
{
    /// <summary>
    /// 任务Key（如果针对特定任务）
    /// </summary>
    public string? JobKey { get; set; }

    /// <summary>
    /// P50 (中位数)
    /// </summary>
    public TimeSpan P50 { get; set; }

    /// <summary>
    /// P90
    /// </summary>
    public TimeSpan P90 { get; set; }

    /// <summary>
    /// P95
    /// </summary>
    public TimeSpan P95 { get; set; }

    /// <summary>
    /// P99
    /// </summary>
    public TimeSpan P99 { get; set; }

    /// <summary>
    /// 平均耗时
    /// </summary>
    public TimeSpan Average { get; set; }

    /// <summary>
    /// 最短耗时
    /// </summary>
    public TimeSpan Min { get; set; }

    /// <summary>
    /// 最长耗时
    /// </summary>
    public TimeSpan Max { get; set; }

    /// <summary>
    /// 样本数量
    /// </summary>
    public int SampleCount { get; set; }
}

/// <summary>
/// 排行榜类型
/// </summary>
public enum RankingType
{
    /// <summary>
    /// 按执行次数排行
    /// </summary>
    ExecutionCount,

    /// <summary>
    /// 按失败次数排行
    /// </summary>
    FailureCount,

    /// <summary>
    /// 按平均耗时排行（从长到短）
    /// </summary>
    AverageDuration,

    /// <summary>
    /// 按跳过次数排行
    /// </summary>
    SkipCount
}

/// <summary>
/// 任务排行榜项
/// </summary>
public class JobRanking
{
    /// <summary>
    /// 排名
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// 任务Key
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// 数值（根据排行类型含义不同）
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// 格式化后的数值显示
    /// </summary>
    public string? FormattedValue { get; set; }

    /// <summary>
    /// 占总数的百分比
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// 失败分析结果
/// </summary>
public class FailureAnalysis
{
    /// <summary>
    /// 按任务分组的失败统计
    /// </summary>
    public List<JobFailureCount> ByJob { get; set; } = [];

    /// <summary>
    /// 重试成功率 (0-100)
    /// 计算公式: 重试后成功的数量 / 总重试次数 * 100
    /// </summary>
    public double RetrySuccessRate { get; set; }

    /// <summary>
    /// 总失败数
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// 总终止数
    /// </summary>
    public int TotalTerminated { get; set; }

    /// <summary>
    /// 统计时间范围起始
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 统计时间范围结束
    /// </summary>
    public DateTime EndTime { get; set; }
}

/// <summary>
/// 任务失败统计
/// </summary>
public class JobFailureCount
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
    /// 失败次数（Failed状态）
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 终止次数（Terminated状态）
    /// </summary>
    public int TerminatedCount { get; set; }

    /// <summary>
    /// 总失败数（Failed + Terminated）
    /// </summary>
    public int TotalFailureCount => FailedCount + TerminatedCount;

    /// <summary>
    /// 该任务的执行总数
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// 失败率 (0-100)
    /// </summary>
    public double FailureRate => TotalExecutions > 0 ? ((double)TotalFailureCount / TotalExecutions) * 100 : 0;
}

/// <summary>
/// 时间范围预设
/// </summary>
public enum TimeRangePreset
{
    /// <summary>
    /// 今天
    /// </summary>
    Today,

    /// <summary>
    /// 最近24小时
    /// </summary>
    Last24Hours,

    /// <summary>
    /// 最近7天
    /// </summary>
    Last7Days,

    /// <summary>
    /// 最近30天
    /// </summary>
    Last30Days,

    /// <summary>
    /// 自定义范围
    /// </summary>
    Custom
}

/// <summary>
/// 最慢实例信息（用于 Top N 展示）
/// </summary>
public class SlowestInstance
{
    /// <summary>
    /// 实例ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// 作业键
    /// </summary>
    public required string JobKey { get; set; }

    /// <summary>
    /// 作业名称
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// 执行耗时
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// 统计页面请求参数
/// </summary>
public class StatisticsRequest
{
    /// <summary>
    /// 时间范围预设
    /// </summary>
    public TimeRangePreset Preset { get; set; } = TimeRangePreset.Today;

    /// <summary>
    /// 自定义起始时间（仅当Preset为Custom时使用）
    /// </summary>
    public DateTime? CustomStartTime { get; set; }

    /// <summary>
    /// 自定义结束时间（仅当Preset为Custom时使用）
    /// </summary>
    public DateTime? CustomEndTime { get; set; }

    /// <summary>
    /// 时间粒度
    /// </summary>
    public TimeGranularity Granularity { get; set; } = TimeGranularity.Hourly;

    /// <summary>
    /// 排行榜显示数量
    /// </summary>
    public int TopN { get; set; } = 10;

    /// <summary>
    /// 获取实际的起始时间
    /// </summary>
    public DateTime GetStartTime()
    {
        if (Preset == TimeRangePreset.Custom && CustomStartTime.HasValue)
            return CustomStartTime.Value;

        var now = DateTime.UtcNow;
        return Preset switch
        {
            TimeRangePreset.Today => now.Date,
            TimeRangePreset.Last24Hours => now.AddHours(-24),
            TimeRangePreset.Last7Days => now.AddDays(-7),
            TimeRangePreset.Last30Days => now.AddDays(-30),
            _ => now.Date
        };
    }

    /// <summary>
    /// 获取实际的结束时间
    /// </summary>
    public DateTime GetEndTime()
    {
        if (Preset == TimeRangePreset.Custom && CustomEndTime.HasValue)
            return CustomEndTime.Value;

        return DateTime.UtcNow;
    }
}
