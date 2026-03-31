using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Profiling.Pages;
using Monica.Profiling.UIExecutionTiming.State;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExecutionTimingUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the execution-timing UI module.
        /// </summary>
        public static ModuleExecutionTimingUIGuide AddExecutionTimingUI(Action<ModuleExecutionTimingUIOption>? action = null)
        {
            return new ModuleExecutionTimingUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Execution-timing UI module.
/// </summary>
[ModuleKey(EMoModuleKey.ExecutionTimingUI)]
public class ModuleExecutionTimingUI(ModuleExecutionTimingUIOption option)
    : MoModule<ModuleExecutionTimingUI, ModuleExecutionTimingUIOption, ModuleExecutionTimingUIGuide>(option)
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

            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIExecutionTimingPage>(
                    UIExecutionTimingPage.PAGE_URL,
                    "Pages:ExecutionTiming:Title",
                    Icons.Material.Filled.Timer,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 30));
        }
    }
}

/// <summary>
/// Configuration guide for the execution-timing UI module.
/// </summary>
public class ModuleExecutionTimingUIGuide
    : MoModuleGuide<ModuleExecutionTimingUI, ModuleExecutionTimingUIOption, ModuleExecutionTimingUIGuide>
{
}

/// <summary>
/// Configuration options for the execution-timing UI module.
/// </summary>
public class ModuleExecutionTimingUIOption : MoModuleOption<ModuleExecutionTimingUI>
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
