using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard.Models;

/// <summary>
/// 表示模块系统的性能信息。
/// </summary>
public class ModuleSystemPerformance
{
    /// <summary>
    /// 系统总的初始化时间（毫秒）
    /// </summary>
    public long TotalSystemInitializationTimeMs { get; set; }

    /// <summary>
    /// 各个阶段的性能信息
    /// </summary>
    public List<PhasePerformanceInfo> PhasePerformances { get; set; } = [];

    /// <summary>
    /// 各个模块的性能信息
    /// </summary>
    public List<ModulePerformanceInfo> ModulePerformances { get; set; } = [];

    /// <summary>
    /// 最慢的5个模块
    /// </summary>
    public List<ModulePerformanceInfo> SlowestModules { get; set; } = [];

    /// <summary>
    /// 各个配置方法阶段的统计信息
    /// </summary>
    public List<ConfigMethodStatistics> ConfigMethodStatistics { get; set; } = [];

    /// <summary>
    /// 所有系统阶段总耗时（毫秒）
    /// </summary>
    public long TotalSystemPhaseDurationMs { get; set; }

    /// <summary>
    /// 所有模块阶段总耗时（毫秒）
    /// </summary>
    public long TotalModulePhaseDurationMs { get; set; }

    /// <summary>
    /// 系统阶段数量
    /// </summary>
    public int SystemPhaseCount { get; set; }

    /// <summary>
    /// 模块阶段执行总次数
    /// </summary>
    public int TotalModulePhaseExecutions { get; set; }
}

/// <summary>
/// 阶段性能信息
/// </summary>
public class PhasePerformanceInfo
{
    /// <summary>
    /// 阶段名称
    /// </summary>
    public string PhaseName { get; set; } = string.Empty;

    /// <summary>
    /// 阶段耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 阶段在初始化顺序中的位置
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// 模块性能信息
/// </summary>
public class ModulePerformanceInfo
{
    /// <summary>
    /// 模块类型名称
    /// </summary>
    public string ModuleTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 模块枚举
    /// </summary>
    public EMoModules ModuleEnum { get; set; }

    /// <summary>
    /// 模块总耗时（毫秒）
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// 各个配置阶段的耗时
    /// </summary>
    public Dictionary<EMoModuleConfigMethods, long> PhaseDurations { get; set; } = [];
}

/// <summary>
/// 配置方法统计信息
/// </summary>
public class ConfigMethodStatistics
{
    /// <summary>
    /// 配置方法类型
    /// </summary>
    public EMoModuleConfigMethods ConfigMethod { get; set; }

    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// 平均耗时（毫秒）
    /// </summary>
    public long AverageDurationMs { get; set; }

    /// <summary>
    /// 执行此配置方法的模块数量
    /// </summary>
    public int ModuleCount { get; set; }

    /// <summary>
    /// 最慢的模块名称
    /// </summary>
    public string SlowestModuleName { get; set; } = string.Empty;

    /// <summary>
    /// 最慢的模块耗时（毫秒）
    /// </summary>
    public long SlowestModuleDurationMs { get; set; }
} 