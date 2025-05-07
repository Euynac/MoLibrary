namespace MoLibrary.Core.Module.TypeFinder;

/// <summary>
/// 模块核心类型查找器配置选项
/// </summary>
public class ModuleCoreOptionTypeFinder
{
    /// <summary>
    /// 相关程序集名称，用于筛选要加载的程序集
    /// </summary>
    public string[] RelatedAssemblies { get; set; } = Array.Empty<string>();
} 