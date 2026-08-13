using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Profiling.Localization;
using Monica.Profiling.Pages;
using Monica.Profiling.UIMemoryAnalysis.State;
using Monica.UI.Shell.Models;
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
        public ModuleRegistration<ModuleMemoryAnalysisUI, ModuleMemoryAnalysisUIOption> AddMemoryAnalysisUI(
            Action<ModuleMemoryAnalysisUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleMemoryAnalysisUI, ModuleMemoryAnalysisUIOption>(action);
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<ProfilingResource>()
                .AddResource<MemoryAnalysisResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIMemoryAnalysisPage, MemoryAnalysisResource>(
                    UIMemoryAnalysisPage.PAGE_URL,
                    "Pages:MemoryAnalysis:Title",
                    Icons.Material.Filled.Memory,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 60));
            return registration;
        }
    }

    extension(ModuleRegistration<ModuleMemoryAnalysisUI, ModuleMemoryAnalysisUIOption> registration)
    {
        /// <summary>
        /// Enables allocation tracking and the type-allocation tab for this host.
        /// </summary>
        /// <returns>The same host-bound memory-analysis registration.</returns>
        public ModuleRegistration<ModuleMemoryAnalysisUI, ModuleMemoryAnalysisUIOption> EnableTypeAllocationTab()
        {
            registration.Require<ModuleTypeAllocation, ModuleTypeAllocationOption>();
            return registration.Configure(option => option.EnableTypeAllocationTab = true);
        }
    }
}

/// <summary>
/// Memory analysis UI module.
/// </summary>
public class ModuleMemoryAnalysisUI : MonicaModule<ModuleMemoryAnalysisUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleMemoryDiagnostics, ModuleMemoryDiagnosticsOption>();
        module.Require<ModuleRuntimeMetrics, ModuleRuntimeMetricsOption>();
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleMemoryAnalysisUIOption> context)
    {
        context.Services.AddScoped<MemoryAnalysisPageStateFactory>();
        if (Option.EnableTypeAllocationTab)
        {
            context.Services.AddScoped<TypeAllocationPanelStateFactory>();
        }
    }
}

/// <summary>
/// Configuration options for the memory analysis UI module.
/// </summary>
public class ModuleMemoryAnalysisUIOption : ModuleOptions<ModuleMemoryAnalysisUI>
{
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
    public bool EnableTypeAllocationTab { get; internal set; }
}
