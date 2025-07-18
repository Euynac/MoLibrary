using MoLibrary.Core.Module.Features;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard.Models;

/// <summary>
/// 表示单个模块的详细信息。
/// </summary>
public class ModuleDetailInfo
{
    /// <summary>
    /// 模块基本信息
    /// </summary>
    public ModuleBasicInfo BasicInfo { get; set; } = new();

    /// <summary>
    /// 模块性能信息
    /// </summary>
    public ModulePerformanceInfo PerformanceInfo { get; set; } = new();

    /// <summary>
    /// 模块依赖关系信息
    /// </summary>
    public ModuleDependencyInfo DependencyInfo { get; set; } = new();

    /// <summary>
    /// 模块配置选项信息
    /// </summary>
    public ModuleConfigInfo ConfigInfo { get; set; } = new();

    /// <summary>
    /// 模块执行历史
    /// </summary>
    public List<ModulePhaseExecution> ExecutionHistory { get; set; } = [];

    /// <summary>
    /// 模块错误信息（如果有）
    /// </summary>
    public List<ModuleErrorInfo> Errors { get; set; } = [];
}

/// <summary>
/// 模块配置信息
/// </summary>
public class ModuleConfigInfo
{
    /// <summary>
    /// 模块是否被禁用
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// 模块禁用原因
    /// </summary>
    public string? DisabledReason { get; set; }

    /// <summary>
    /// 模块配置项
    /// </summary>
    public Dictionary<string, object?> ConfigurationItems { get; set; } = [];

    /// <summary>
    /// 模块注册请求数量
    /// </summary>
    public int RegisterRequestCount { get; set; }

    /// <summary>
    /// 模块是否具有循环依赖
    /// </summary>
    public bool HasCircularDependency { get; set; }
}

/// <summary>
/// 模块阶段执行信息
/// </summary>
public class ModulePhaseExecution
{
    /// <summary>
    /// 执行阶段
    /// </summary>
    public EMoModuleConfigMethods Phase { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 执行状态
    /// </summary>
    public PhaseExecutionStatus Status { get; set; }

    /// <summary>
    /// 错误信息（如果有）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 模块错误信息
/// </summary>
public class ModuleErrorInfo
{
    /// <summary>
    /// 错误类型
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 发生错误的阶段
    /// </summary>
    public EMoModuleConfigMethods? Phase { get; set; }

    /// <summary>
    /// 错误发生时间
    /// </summary>
    public DateTime ErrorTime { get; set; }

    /// <summary>
    /// 错误堆栈信息
    /// </summary>
    public string? StackTrace { get; set; }
}

/// <summary>
/// 阶段执行状态
/// </summary>
public enum PhaseExecutionStatus
{
    /// <summary>
    /// 未开始
    /// </summary>
    NotStarted,

    /// <summary>
    /// 执行中
    /// </summary>
    Running,

    /// <summary>
    /// 执行成功
    /// </summary>
    Completed,

    /// <summary>
    /// 执行失败
    /// </summary>
    Failed,

    /// <summary>
    /// 被跳过
    /// </summary>
    Skipped
} 