using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.UI.UIStackTrace.Support;


// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleStackTraceUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the stack trace UI module.
        /// </summary>
        public ModuleStackTraceUIGuide AddStackTraceUI(Action<ModuleStackTraceUIOption>? action = null)
        {
            return builder.AddModule<ModuleStackTraceUI, ModuleStackTraceUIOption, ModuleStackTraceUIGuide>(action);
        }
    }
}

/// <summary>
/// UIStackTrace module - provides stack trace visualization components
/// </summary>
[ModuleKey(BuiltInModuleKey.UIStackTrace)]
public class ModuleStackTraceUI(ModuleStackTraceUIOption option)
    : ModuleBase<ModuleStackTraceUI, ModuleStackTraceUIOption, ModuleStackTraceUIGuide>(option)
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
    : ModuleGuide<ModuleStackTraceUI, ModuleStackTraceUIOption, ModuleStackTraceUIGuide>
{
}

/// <summary>
/// UIStackTrace module options
/// </summary>
public class ModuleStackTraceUIOption : ModuleOptions<ModuleStackTraceUI>
{
}
