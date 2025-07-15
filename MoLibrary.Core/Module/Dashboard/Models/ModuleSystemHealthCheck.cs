using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard.Models;

/// <summary>
/// 表示模块系统的健康状态检查结果。
/// </summary>
public class ModuleSystemHealthCheck
{
    /// <summary>
    /// 整体健康状态
    /// </summary>
    public HealthStatus OverallHealth { get; set; }

    /// <summary>
    /// 健康检查摘要
    /// </summary>
    public string HealthSummary { get; set; } = string.Empty;

    /// <summary>
    /// 检查时间
    /// </summary>
    public DateTime CheckTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 各项健康检查项目
    /// </summary>
    public List<HealthCheckItem> HealthCheckItems { get; set; } = [];

    /// <summary>
    /// 发现的问题列表
    /// </summary>
    public List<HealthIssue> Issues { get; set; } = [];

    /// <summary>
    /// 建议操作
    /// </summary>
    public List<string> Recommendations { get; set; } = [];

    /// <summary>
    /// 性能指标
    /// </summary>
    public HealthPerformanceMetrics PerformanceMetrics { get; set; } = new();
}

/// <summary>
/// 健康检查项目
/// </summary>
public class HealthCheckItem
{
    /// <summary>
    /// 检查项目名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 检查项目描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 检查状态
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// 检查结果详情
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// 检查执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 相关的模块（如果适用）
    /// </summary>
    public EMoModules? RelatedModule { get; set; }
}

/// <summary>
/// 健康问题
/// </summary>
public class HealthIssue
{
    /// <summary>
    /// 问题严重程度
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// 问题标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 问题描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 相关的模块
    /// </summary>
    public EMoModules? AffectedModule { get; set; }

    /// <summary>
    /// 问题类型
    /// </summary>
    public IssueType IssueType { get; set; }

    /// <summary>
    /// 推荐解决方案
    /// </summary>
    public string? RecommendedAction { get; set; }

    /// <summary>
    /// 发现时间
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 健康性能指标
/// </summary>
public class HealthPerformanceMetrics
{
    /// <summary>
    /// 平均模块初始化时间（毫秒）
    /// </summary>
    public double AverageModuleInitTimeMs { get; set; }

    /// <summary>
    /// 最慢模块初始化时间（毫秒）
    /// </summary>
    public long SlowestModuleInitTimeMs { get; set; }

    /// <summary>
    /// 最慢模块名称
    /// </summary>
    public string? SlowestModuleName { get; set; }

    /// <summary>
    /// 总系统初始化时间（毫秒）
    /// </summary>
    public long TotalSystemInitTimeMs { get; set; }

    /// <summary>
    /// 模块初始化效率评分（0-100）
    /// </summary>
    public int InitializationEfficiencyScore { get; set; }

    /// <summary>
    /// 内存使用情况（如果可用）
    /// </summary>
    public long? MemoryUsageBytes { get; set; }
}

/// <summary>
/// 健康状态
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// 健康
    /// </summary>
    Healthy,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 不健康
    /// </summary>
    Unhealthy,

    /// <summary>
    /// 严重错误
    /// </summary>
    Critical,

    /// <summary>
    /// 未知状态
    /// </summary>
    Unknown
}

/// <summary>
/// 问题严重程度
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// 信息
    /// </summary>
    Information,

    /// <summary>
    /// 低
    /// </summary>
    Low,

    /// <summary>
    /// 中等
    /// </summary>
    Medium,

    /// <summary>
    /// 高
    /// </summary>
    High,

    /// <summary>
    /// 严重
    /// </summary>
    Critical
}

/// <summary>
/// 问题类型
/// </summary>
public enum IssueType
{
    /// <summary>
    /// 配置问题
    /// </summary>
    Configuration,

    /// <summary>
    /// 性能问题
    /// </summary>
    Performance,

    /// <summary>
    /// 依赖问题
    /// </summary>
    Dependency,

    /// <summary>
    /// 初始化错误
    /// </summary>
    Initialization,

    /// <summary>
    /// 内存问题
    /// </summary>
    Memory,

    /// <summary>
    /// 其他
    /// </summary>
    Other
} 