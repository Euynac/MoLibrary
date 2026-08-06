using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        public ModuleRegistration<ModuleStackTraceUI, ModuleStackTraceUIOption> AddStackTraceUI(
            Action<ModuleStackTraceUIOption>? action = null)
        {
            return builder.AddModule<ModuleStackTraceUI, ModuleStackTraceUIOption>(action);
        }
    }
}

/// <summary>
/// UIStackTrace module - provides stack trace visualization components
/// </summary>
public class ModuleStackTraceUI : MonicaModule<ModuleStackTraceUIOption>, IUIModule
{
    public override void ConfigureServices(ModuleContext<ModuleStackTraceUIOption> context)
    {
        // Register StackTraceParserService as Singleton (stateless parser)
        context.Services.AddSingleton<StackTraceParser>();
    }
}

/// <summary>
/// UIStackTrace module options
/// </summary>
public class ModuleStackTraceUIOption : ModuleOptions<ModuleStackTraceUI>
{
}
