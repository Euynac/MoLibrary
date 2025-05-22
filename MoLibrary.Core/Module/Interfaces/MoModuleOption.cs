using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Core.Module.BuilderWrapper;

namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleOption<TModule> : IMoModuleOption<TModule> where TModule : IMoModule
{
    /// <summary>
    /// 一般用于模块注册期间日志
    /// </summary>
    public ILogger Logger { get; set; } = LogProvider.For<TModule>(ModuleCoreOption.DefaultModuleLogLevel);

    /// <summary>
    /// 模块立即进行注册，如一些模块有需要在注册期间进行使用的，如Configuration、Logging模块等。需要使用 <see cref="WebApplicationBuilderExtensions.ConfigMoModule"/> 设定 <see cref="ModuleCoreOption.EnableRegisterInstantly"/> 以生效
    /// </summary>
    public bool RegisterInstantly { get; set; }

    /// <summary>
    /// 如果模块注册出现异常则禁用Module，而不是抛出异常
    /// </summary>
    public static bool DisableModuleIfHasException { get; set; } 

    /// <summary>
    /// Sets the minimum log level for the module logger.
    /// </summary>
    /// <param name="logLevel">The minimum log level to set for this module.</param>
    public void SetModuleLogLevel(LogLevel logLevel)
    {
        Logger = LogProvider.For<TModule>(logLevel);
    }

    /// <summary>
    /// Disables the module log.
    /// </summary>
    public void DisableModuleLog()
    {
        Logger = NullLogger.Instance;
    }
}