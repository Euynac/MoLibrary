using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.DependencyInjection.Modules;

public class ModuleDependencyInjectionOption : MoModuleOption<ModuleDependencyInjection>
{
    /// <summary>
    /// 相关项目单元所在程序集名，使用名称包含查找。如若不配置，则默认仅扫描Entry程序集。
    /// </summary>
    public string[]? RelatedAssemblies { get; set; }
    public bool EnableDebug { get; set; }
}

