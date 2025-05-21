using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Framework.Core.Model;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Modules;

public class ModuleFrameworkMonitorOption : MoModuleControllerOption<ModuleFrameworkMonitor>
{
    /// <summary>
    /// 惯例命名设置
    /// </summary>
    public UnitNameConventionOptions ConventionOptions { get; set; } = new();

    /// <summary>
    /// 开启请求过滤器
    /// </summary>
    public bool EnableRequestFilter { get; set; }
}


public class UnitNameConventionOptions
{
    public Dictionary<EProjectUnitType, UnitNameConventionOption> Dict { get; set; } = [];
    /// <summary>
    /// 全局惯例命名模式
    /// </summary>
    public ENameConventionMode NameConventionMode { get; set; } = ENameConventionMode.Warning;

    /// <summary>
    /// 使用惯例名称检查
    /// </summary>
    public bool EnableNameConvention { get; set; }
    /// <summary>
    /// 开启性能模式（即先检查命名，但并不一定性能更好）（注意，开启性能模式后，名称检查失败后提醒模式将失效）
    /// </summary>
    public bool EnablePerformanceMode { get; set; }
}

public class UnitNameConventionOption
{
    /// <summary>
    /// 后缀名
    /// </summary>
    public string? Postfix { get; set; }
    /// <summary>
    /// 前缀名
    /// </summary>
    public string? Prefix { get; set; }
    /// <summary>
    /// 包含名
    /// </summary>
    public string? Contains { get; set; }
    /// <summary>
    /// 命名空间包含，如必须放入文件夹名
    /// </summary>
    public string? NamespaceContains { get; set; }
    /// <summary>
    /// 惯例命名模式，不设置使用全局模式
    /// </summary>
    public ENameConventionMode? NameConventionMode { get; set; } = ENameConventionMode.Warning;

    public override string ToString()
    {
        return $"{Postfix?.Be("后缀：{0}\n", true)}{Prefix?.Be("前缀：{0}\n", true)}{Contains?.Be("包含：{0}", true)}{NamespaceContains?.Be("命名空间包含：{0}", true)}".TrimEnd();
    }
}

public enum ENameConventionMode
{
    /// <summary>
    /// 警告模式，仅提醒
    /// </summary>
    Warning,
    /// <summary>
    /// 严格模式，错误直接无法运行
    /// </summary>
    Strict,
    /// <summary>
    /// 禁用Convention
    /// </summary>
    Disable
}