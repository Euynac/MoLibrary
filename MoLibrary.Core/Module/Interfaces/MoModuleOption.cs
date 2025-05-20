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
    public ILogger Logger { get; set; } = LogProvider.For<TModule>();

    /// <summary>
    /// 模块立即进行注册，如一些模块有需要在注册期间进行使用的，如Configuration、Logging模块等。需要使用 <see cref="WebApplicationBuilderExtensions.ConfigMoModule"/> 设定 <see cref="ModuleCoreOption.EnableRegisterInstantly"/> 以生效
    /// </summary>
    public bool RegisterInstantly { get; set; }

    public void DisableModuleLog()
    {
        Logger = NullLogger.Instance;
    }
}