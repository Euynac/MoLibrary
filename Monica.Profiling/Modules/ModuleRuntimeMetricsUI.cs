using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Profiling.Localization;
using Monica.Profiling.Pages;
using Monica.Profiling.UIRuntimeMetrics.State;
using Monica.UI.Shell.Models;
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
        public ModuleRegistration<ModuleRuntimeMetricsUI, ModuleRuntimeMetricsUIOption> AddRuntimeMetricsUI(
            Action<ModuleRuntimeMetricsUIOption>? action = null)
        {
            return builder.AddModule<ModuleRuntimeMetricsUI, ModuleRuntimeMetricsUIOption>(action);
        }
    }
}

/// <summary>
/// Runtime metrics UI module.
/// </summary>
public class ModuleRuntimeMetricsUI : MonicaModule<ModuleRuntimeMetricsUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleRuntimeMetrics, ModuleRuntimeMetricsOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>(
            static option => option.AddResource<RuntimeMetricsResource>());
        module.Require<ModuleShellUI, ModuleShellUIOption>(static option =>
            option.ConfigureNavigation(static registry =>
                registry.RegisterLocalizedPage<UIRuntimeMetricsPage, RuntimeMetricsResource>(
                    UIRuntimeMetricsPage.PAGE_URL,
                    "Pages:RuntimeMetrics:Title",
                    Icons.Material.Filled.Speed,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 10)));
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleRuntimeMetricsUIOption> context)
    {
        context.Services.AddScoped<RuntimeMetricsPageState>();
    }
}

/// <summary>
/// Configuration options for the runtime metrics UI module.
/// </summary>
public class ModuleRuntimeMetricsUIOption : ModuleOptions<ModuleRuntimeMetricsUI>
{
    /// <summary>
    /// Controls the automatic refresh interval of the runtime metrics page in milliseconds.
    /// Set this to 0 to disable timer-based refresh and require manual refresh only.
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 2000;
}
