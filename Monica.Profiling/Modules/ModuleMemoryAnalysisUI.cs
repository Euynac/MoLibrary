using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Profiling.Pages;
using Monica.Profiling.UIMemoryAnalysis.State;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the memory analysis UI module.
/// </summary>
public static class ModuleMemoryAnalysisUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the memory analysis UI module.
        /// </summary>
        public ModuleMemoryAnalysisUIGuide AddMemoryAnalysisUI(Action<ModuleMemoryAnalysisUIOption>? action = null)
        {
            return builder.AddModule<ModuleMemoryAnalysisUI, ModuleMemoryAnalysisUIOption, ModuleMemoryAnalysisUIGuide>(action);
        }
    }
}

/// <summary>
/// Memory analysis UI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.MemoryAnalysisUI)]
public class ModuleMemoryAnalysisUI(ModuleMemoryAnalysisUIOption option)
    : ModuleBase<ModuleMemoryAnalysisUI, ModuleMemoryAnalysisUIOption, ModuleMemoryAnalysisUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        if (Option.DisableMemoryAnalysisPage)
        {
            return;
        }

        DependsOnModule<ModuleMemoryDiagnosticsGuide>().Register();
        DependsOnModule<ModuleRuntimeMetricsGuide>().Register();

        if (Option.EnableTypeAllocationTab)
        {
            DependsOnModule<ModuleTypeAllocationGuide>().Register();
        }

        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UIMemoryAnalysisPage>(
                UIMemoryAnalysisPage.PAGE_URL,
                "Pages:MemoryAnalysis:Title",
                Icons.Material.Filled.Memory,
                "Categories:Monitor",
                addToNav: true,
                navOrder: 60));
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<MemoryAnalysisPageState>();
        services.AddScoped<TypeAllocationPanelState>();
    }
}

/// <summary>
/// Fluent guide for the memory analysis UI module.
/// </summary>
public class ModuleMemoryAnalysisUIGuide
    : ModuleGuide<ModuleMemoryAnalysisUI, ModuleMemoryAnalysisUIOption, ModuleMemoryAnalysisUIGuide>
{
}

/// <summary>
/// Configuration options for the memory analysis UI module.
/// </summary>
public class ModuleMemoryAnalysisUIOption : ModuleOptions<ModuleMemoryAnalysisUI>
{
    /// <summary>
    /// Disables registration of the memory analysis page and removes it from the navigation registry.
    /// </summary>
    public bool DisableMemoryAnalysisPage { get; set; }

    /// <summary>
    /// Controls the dashboard refresh interval in milliseconds.
    /// Set this to 0 to disable timer-based refresh and require manual refresh only.
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Enables the manual GC action button in the diagnostics UI.
    /// Disable this when operators should observe memory behavior without forcing collections.
    /// </summary>
    public bool AllowManualGC { get; set; } = true;

    /// <summary>
    /// Enables GC dump creation from the diagnostics UI.
    /// Disable this if operators must not generate diagnostic dump files from the browser.
    /// </summary>
    public bool AllowGcDump { get; set; } = true;

    /// <summary>
    /// Enables the type allocation tab and registers the type allocation backend dependency for the page.
    /// Keep this disabled when the memory analysis view should expose only memory snapshots and GC diagnostics.
    /// </summary>
    public bool EnableTypeAllocationTab { get; set; }
}
