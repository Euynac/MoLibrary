namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 模块注册错误信息，用于记录模块注册过程中的错误
/// </summary>
public class ModuleRegisterError
{
    /// <summary>
    /// 模块类型
    /// </summary>
    public required Type ModuleType { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public required string ErrorMessage { get; set; }

    /// <summary>
    /// 配置来源
    /// </summary>
    public EMoModules? GuideFrom { get; set; }

    /// <summary>
    /// 未配置的方法键
    /// </summary>
    public List<string> MissingConfigKeys { get; set; } = [];
}