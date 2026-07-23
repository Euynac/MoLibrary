using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Profiling.Localization;
using Monica.Profiling.Pages;
using Monica.Profiling.UIExecutionTiming.State;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExecutionTimingUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the execution-timing UI module.
        /// </summary>
        public ModuleExecutionTimingUIGuide AddExecutionTimingUI(Action<ModuleExecutionTimingUIOption>? action = null)
        {
            return builder.AddModule<ModuleExecutionTimingUI, ModuleExecutionTimingUIOption, ModuleExecutionTimingUIGuide>(action);
        }
    }
}

/// <summary>
/// Execution-timing UI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.ExecutionTimingUI)]
public class ModuleExecutionTimingUI(ModuleExecutionTimingUIOption option)
    : ModuleBase<ModuleExecutionTimingUI, ModuleExecutionTimingUIOption, ModuleExecutionTimingUIGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ExecutionTimingPageState>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableExecutionTimingPage)
        {
            DependsOnModule<ModuleExecutionTimingGuide>().Register();
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<ExecutionTimingResource>();

            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedPage<UIExecutionTimingPage, ExecutionTimingResource>(
                    UIExecutionTimingPage.PAGE_URL,
                    "Pages:ExecutionTiming:Title",
                    Icons.Material.Filled.Timer,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 30));
        }
    }
}

/// <summary>
/// Configuration guide for the execution-timing UI module.
/// </summary>
public class ModuleExecutionTimingUIGuide
    : ModuleGuide<ModuleExecutionTimingUI, ModuleExecutionTimingUIOption, ModuleExecutionTimingUIGuide>
{
}

/// <summary>
/// Configuration options for the execution-timing UI module.
/// </summary>
public class ModuleExecutionTimingUIOption : ModuleOptions<ModuleExecutionTimingUI>
{
    /// <summary>
    /// Disables registration of the execution-timing page and removes it from the UI navigation registry.
    /// </summary>
    public bool DisableExecutionTimingPage { get; set; }

    /// <summary>
    /// Controls the automatic refresh interval of the execution-timing page in milliseconds.
    /// Set this to 0 to disable timer-based refresh and require manual refresh only.
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 2000;
}
