using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Core.Modules;

/// <summary>
/// 异常池模块配置选项
/// </summary>
public class ModuleExceptionPoolOption : MoModuleOption<ModuleExceptionPool>
{
    /// <summary>
    /// 全局异常池配置选项
    /// </summary>
    public ExceptionPoolGlobalOption GlobalOption { get; set; } = new();
}
