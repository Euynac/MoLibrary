namespace MoLibrary.Core.Module.Dashboard.Models;

/// <summary>
/// 表示模块系统的整体状态信息。
/// </summary>
public class ModuleSystemStatus
{
    /// <summary>
    /// 系统是否已经启动完成
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// 总的模块数量
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
    /// 注册错误的模块数量
    /// </summary>
    public int ErrorModules { get; set; }

    /// <summary>
    /// 系统总的初始化时间（毫秒）
    /// </summary>
    public long TotalInitializationTimeMs { get; set; }

    /// <summary>
    /// 系统启动时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 系统完成初始化时间
    /// </summary>
    public DateTime? CompletionTime { get; set; }

    /// <summary>
    /// 模块系统当前状态
    /// </summary>
    public ModuleSystemState State { get; set; }

    /// <summary>
    /// 是否存在循环依赖
    /// </summary>
    public bool HasCircularDependencies { get; set; }

    /// <summary>
    /// 是否存在注册错误
    /// </summary>
    public bool HasRegistrationErrors { get; set; }
}

/// <summary>
/// 模块系统状态枚举
/// </summary>
public enum ModuleSystemState
{
    /// <summary>
    /// 未初始化
    /// </summary>
    NotInitialized,

    /// <summary>
    /// 初始化中
    /// </summary>
    Initializing,

    /// <summary>
    /// 初始化完成
    /// </summary>
    Initialized,

    /// <summary>
    /// 初始化失败
    /// </summary>
    Failed
} 