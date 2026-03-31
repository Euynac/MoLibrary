using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Profiling.Pages;
using Monica.Profiling.UIProfiling.State;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProfilingUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the ProfilingUI module
        /// </summary>
        public static ModuleProfilingUIGuide AddProfilingUI(Action<ModuleProfilingUIOption>? action = null)
        {
            return new ModuleProfilingUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Profiling UI module - provides memory analysis and performance monitoring interface
/// </summary>
[ModuleKey(EMoModuleKey.ProfilingUI)]
public class ModuleProfilingUI(ModuleProfilingUIOption option)
    : MoModule<ModuleProfilingUI, ModuleProfilingUIOption, ModuleProfilingUIGuide>(option)
{

    public override void ClaimDependencies()
    {
        if (!Option.DisableProfilingPages)
        {
            DependsOnModule<ModuleProfilingGuide>().Register();

            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedComponent<UIQuickMonitorPage>(
                        UIQuickMonitorPage.PAGE_URL,
                        "Pages:QuickMonitor:Title",
                        Icons.Material.Filled.Speed,
                        "Categories:Monitor",
                        addToNav: true,
                        navOrder: 10);

                    registry.RegisterLocalizedComponent<UIProfilingDashboardPage>(
                        UIProfilingDashboardPage.PAGE_URL,
                        "Pages:ProfilingDashboard:Title",
                        Icons.Material.Filled.Memory,
                        "Categories:Monitor",
                        addToNav: true,
                        navOrder: 60);
                });
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ProfilingMonitorPageState>();
        services.AddScoped<ProfilingDashboardPageState>();
        services.AddScoped<TypeAllocationPanelState>();
    }
}

/// <summary>
/// Profiling UI module configuration guide
/// </summary>
public class ModuleProfilingUIGuide
    : MoModuleGuide<ModuleProfilingUI, ModuleProfilingUIOption, ModuleProfilingUIGuide>
{
}

/// <summary>
/// Profiling UI module options
/// </summary>
public class ModuleProfilingUIOption : MoModuleOption<ModuleProfilingUI>
{
    /// <summary>
    /// Disables registration of the profiling UI pages and removes them from the navigation registry.
    /// </summary>
    public bool DisableProfilingPages { get; set; }

    /// <summary>
    /// Controls the dashboard and quick-monitor refresh interval in milliseconds.
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
}
