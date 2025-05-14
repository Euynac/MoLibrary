using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;

namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleOption<TModule> : IMoModuleOption<TModule> where TModule : IMoModule
{
    /// <summary>
    /// 一般用于模块注册期间日志
    /// </summary>
    public ILogger Logger { get; set; } = LogProvider.For<TModule>();

    public void DisableModuleLog()
    {
        Logger = NullLogger.Instance;
    }
}