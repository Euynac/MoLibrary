using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.UI.UIStackTrace.Support;


// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleStackTraceUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the stack trace UI module.
        /// </summary>
        public static ModuleStackTraceUIGuide AddStackTraceUI(Action<ModuleStackTraceUIOption>? action = null)
        {
            return new ModuleStackTraceUIGuide().Register(action);
        }
    }
}

/// <summary>
/// UIStackTrace module - provides stack trace visualization components
/// </summary>
[ModuleKey(EMoModuleKey.UIStackTrace)]
public class ModuleStackTraceUI(ModuleStackTraceUIOption option)
    : MoModule<ModuleStackTraceUI, ModuleStackTraceUIOption, ModuleStackTraceUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register StackTraceParserService as Singleton (stateless parser)
        services.AddSingleton<StackTraceParser>();
    }
}

/// <summary>
/// UIStackTrace module configuration guide
/// </summary>
public class ModuleStackTraceUIGuide
    : MoModuleGuide<ModuleStackTraceUI, ModuleStackTraceUIOption, ModuleStackTraceUIGuide>
{
}

/// <summary>
/// UIStackTrace module options
/// </summary>
public class ModuleStackTraceUIOption : MoModuleOption<ModuleStackTraceUI>
{
}
