using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.UI.UIStackTrace.Services;

namespace MoLibrary.UI.Modules;

public static class ModuleUIStackTraceBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 UIStackTrace 模块
        /// </summary>
        public static ModuleUIStackTraceGuide AddUIStackTrace(Action<ModuleUIStackTraceOption>? action = null)
        {
            return new ModuleUIStackTraceGuide().Register(action);
        }
    }
}

/// <summary>
/// UIStackTrace module - provides stack trace visualization components
/// </summary>
public class ModuleUIStackTrace(ModuleUIStackTraceOption option)
    : MoModule<ModuleUIStackTrace, ModuleUIStackTraceOption, ModuleUIStackTraceGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.UIStackTrace;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register StackTraceParserService as Singleton (stateless parser)
        services.AddSingleton<StackTraceParserService>();
    }
}

/// <summary>
/// UIStackTrace module configuration guide
/// </summary>
public class ModuleUIStackTraceGuide
    : MoModuleGuide<ModuleUIStackTrace, ModuleUIStackTraceOption, ModuleUIStackTraceGuide>
{
}

/// <summary>
/// UIStackTrace module options
/// </summary>
public class ModuleUIStackTraceOption : MoModuleOption<ModuleUIStackTrace>
{
}
