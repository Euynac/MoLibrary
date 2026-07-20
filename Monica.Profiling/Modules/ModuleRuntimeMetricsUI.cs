using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Profiling.Pages;
using Monica.Profiling.UIRuntimeMetrics.State;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the runtime metrics UI module.
/// </summary>
public static class ModuleRuntimeMetricsUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the runtime metrics UI module.
        /// </summary>
        public ModuleRuntimeMetricsUIGuide AddRuntimeMetricsUI(Action<ModuleRuntimeMetricsUIOption>? action = null)
        {
            return builder.AddModule<ModuleRuntimeMetricsUI, ModuleRuntimeMetricsUIOption, ModuleRuntimeMetricsUIGuide>(action);
        }
    }
}

/// <summary>
/// Runtime metrics UI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.RuntimeMetricsUI)]
public class ModuleRuntimeMetricsUI(ModuleRuntimeMetricsUIOption option)
    : ModuleBase<ModuleRuntimeMetricsUI, ModuleRuntimeMetricsUIOption, ModuleRuntimeMetricsUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        if (Option.DisableRuntimeMetricsPage)
        {
            return;
        }

        DependsOnModule<ModuleRuntimeMetricsGuide>().Register();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UIRuntimeMetricsPage>(
                UIRuntimeMetricsPage.PAGE_URL,
                "Pages:RuntimeMetrics:Title",
                Icons.Material.Filled.Speed,
                "Categories:Monitor",
                addToNav: true,
                navOrder: 10));
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RuntimeMetricsPageState>();
    }
}

/// <summary>
/// Fluent guide for the runtime metrics UI module.
/// </summary>
public class ModuleRuntimeMetricsUIGuide
    : ModuleGuide<ModuleRuntimeMetricsUI, ModuleRuntimeMetricsUIOption, ModuleRuntimeMetricsUIGuide>
{
}

/// <summary>
/// Configuration options for the runtime metrics UI module.
/// </summary>
public class ModuleRuntimeMetricsUIOption : ModuleOptions<ModuleRuntimeMetricsUI>
{
    /// <summary>
    /// Disables registration of the runtime metrics page and removes it from the navigation registry.
    /// </summary>
    public bool DisableRuntimeMetricsPage { get; set; }

    /// <summary>
    /// Controls the automatic refresh interval of the runtime metrics page in milliseconds.
    /// Set this to 0 to disable timer-based refresh and require manual refresh only.
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 2000;
}
