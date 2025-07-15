using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard.Models;

/// <summary>
/// 表示模块注册信息。
/// </summary>
public class ModuleRegistrationInfo
{
    /// <summary>
    /// 启用的模块列表
    /// </summary>
    public List<ModuleBasicInfo> EnabledModules { get; set; } = [];

    /// <summary>
    /// 禁用的模块列表
    /// </summary>
    public List<ModuleBasicInfo> DisabledModules { get; set; } = [];

    /// <summary>
    /// 模块注册顺序映射（按Order排序）
    /// </summary>
    public Dictionary<int, ModuleBasicInfo> ModulesByOrder { get; set; } = [];

    /// <summary>
    /// 注册统计信息
    /// </summary>
    public ModuleRegistrationStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 模块基本信息
/// </summary>
public class ModuleBasicInfo
{
    /// <summary>
    /// 模块类型名称
    /// </summary>
    public string ModuleTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 模块完整类型名称
    /// </summary>
    public string ModuleFullTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 模块枚举
    /// </summary>
    public EMoModules ModuleEnum { get; set; }

    /// <summary>
    /// 注册顺序
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 模块状态
    /// </summary>
    public EMoModuleConfigMethods Status { get; set; }

    /// <summary>
    /// 直接依赖的模块列表
    /// </summary>
    public List<EMoModules> Dependencies { get; set; } = [];

    /// <summary>
    /// 初始化耗时（毫秒）
    /// </summary>
    public long InitializationTimeMs { get; set; }

    /// <summary>
    /// 是否是禁用状态
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// 是否有注册错误
    /// </summary>
    public bool HasErrors { get; set; }
}

/// <summary>
/// 模块注册统计信息
/// </summary>
public class ModuleRegistrationStatistics
{
    /// <summary>
    /// 总模块数量
    /// </summary>
    public int TotalModules { get; set; }

    /// <summary>
    /// 启用的模块数量
    /// </summary>
    public int EnabledModules { get; set; }

    /// <summary>
    /// 禁用的模块数量
    /// </summary>
    public int DisabledModules { get; set; }

    /// <summary>
    /// 总初始化时间（毫秒）
    /// </summary>
    public long TotalInitializationTimeMs { get; set; }

    /// <summary>
    /// 最慢的5个模块
    /// </summary>
    public List<ModuleBasicInfo> SlowestModules { get; set; } = [];
} 