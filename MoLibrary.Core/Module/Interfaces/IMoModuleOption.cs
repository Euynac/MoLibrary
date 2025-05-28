using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.BuilderWrapper;

namespace MoLibrary.Core.Module.Interfaces;

public interface IMoModuleOption : IMoModuleOptionBase
{
    /// <summary>
    /// 一般用于模块注册期间日志
    /// </summary>
    ILogger Logger { get; set; }

    /// <summary>
    /// 模块立即进行注册，如一些模块有需要在注册期间进行使用的，如Configuration、Logging模块等。需要使用 <see cref="WebApplicationBuilderExtensions.ConfigMoModule"/> 设定 <see cref="ModuleCoreOption.EnableRegisterInstantly"/> 以生效
    /// </summary>
    bool RegisterInstantly { get; set; }

    /// <summary>
    /// 如果模块注册出现异常则禁用Module，而不是抛出异常。
    /// 当设置为 true 时，如果模块在注册过程中出现异常，系统将记录错误并禁用该模块，而不是抛出异常中断整个应用程序的启动。
    /// 被禁用的模块在应用程序的生命周期内将被完全跳过，不会调用其任何配置或初始化方法。
    /// </summary>
    bool? DisableModuleIfHasException { get; set; }
    
    /// <summary>
    /// 是否禁用当前模块。
    /// 当为 true 时，该模块将不会被注册或初始化，在应用程序的整个生命周期中都会被跳过。
    /// </summary>
    bool IsDisabled { get; }
    
    /// <summary>
    /// 手动禁用当前模块。
    /// 被禁用的模块在应用程序的生命周期内将被完全跳过，不会调用其任何配置或初始化方法。
    /// </summary>
    /// <param name="reason">禁用模块的原因，将被记录到日志中</param>
    void DisableModule(string reason = "Manual disable");

    /// <summary>
    /// Sets the minimum log level for the module logger.
    /// </summary>
    /// <param name="logLevel">The minimum log level to set for this module.</param>
    void SetModuleLogLevel(LogLevel logLevel);

    /// <summary>
    /// Disables the module log.
    /// </summary>
    void DisableModuleLog();
}


public interface IMoModuleOptionBase
{

}

public interface IMoModuleOption<TModule> : IMoModuleOption where TModule : IMoModule
{

}

public interface IMoModuleExtraOption<TModule> : IMoModuleOptionBase where TModule : IMoModule
{
}


