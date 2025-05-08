using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Core.Modules;

public class ModuleMapperOption : MoModuleControllerOption<ModuleMapper>
{
    public ILogger Logger { get; set; } = NullLogger.Instance;
    /// <summary>
    /// 启用对Mapper进行调试（暂时仅支持手动调试）
    /// </summary>
    public bool DebugMapper { get; set; } = false;

    /// <summary>
    /// 调试需要传入Mapper定义时涉及的基类或扩展方法相关定义的程序集
    /// </summary>
    public Assembly[]? DebuggerRelatedAssemblies { get; set; }
}