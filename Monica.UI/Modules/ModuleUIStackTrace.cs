using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.UI.UIStackTrace.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleUIStackTraceBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the UIStackTrace module
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
[ModuleKey(EMoModuleKey.UIStackTrace)]
public class ModuleUIStackTrace(ModuleUIStackTraceOption option)
    : MoModule<ModuleUIStackTrace, ModuleUIStackTraceOption, ModuleUIStackTraceGuide>(option)
{

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
