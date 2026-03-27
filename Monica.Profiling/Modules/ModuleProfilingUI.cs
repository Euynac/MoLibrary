using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Profiling.Models;
using Monica.Profiling.Pages;
using Monica.Profiling.Services;
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
        if (!Option.DisableProfilingPage)
        {
            // Depends on Profiling module
            DependsOnModule<ModuleProfilingGuide>().Register();

            // Depend on the UI core module and register UI components
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
        // ProfilingMetricsCollector is registered in ModuleProfiling

        // Register the memory analysis service as Scoped
        services.AddScoped<IMemoryAnalysisService, MemoryAnalysisService>();

        // Register type allocation tracking service (if enabled)
        if (Option.EnableTypeAllocationTracking)
        {
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TypeAllocationCollector>>();
                return new TypeAllocationCollector(
                    logger,
                    maxTrackedTypes: Option.MaxTrackedTypes,
                    autoStopAfter: Option.AutoStopAfter);
            });
            services.AddScoped<ITypeAllocationService, TypeAllocationService>();

            // Register the autostart service (if enabled)
            if (Option.AutoStartCollection)
            {
                services.AddHostedService<TypeAllocationAutoStartService>();
            }
        }
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
    /// Disable Profiling management page
    /// </summary>
    public bool DisableProfilingPage { get; set; } = false;

    /// <summary>
    /// Auto-refresh interval (milliseconds), 0 means auto-refresh is disabled
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Maximum number of historical data points
    /// </summary>
    public int MaxHistoryPoints { get; set; } = 300;

    /// <summary>
    /// Allow manual triggering of GC
    /// </summary>
    public bool AllowManualGC { get; set; } = true;

    /// <summary>
    /// Allow GC Dump generation
    /// </summary>
    public bool AllowGcDump { get; set; } = true;

    /// <summary>
    /// Enable type assignment tracking
    /// </summary>
    public bool EnableTypeAllocationTracking { get; set; } = true;

    /// <summary>
    /// Automatically start collecting distribution events when the application starts
    /// </summary>
    public bool AutoStartCollection { get; set; } = false;

    /// <summary>
    /// Default sampling mode (for automatic startup)
    /// </summary>
    public AllocationSamplingMode DefaultSamplingMode { get; set; } = AllocationSamplingMode.High;

    /// <summary>
    /// Maximum number of tracking types
    /// </summary>
    public int MaxTrackedTypes { get; set; } = 500;

    /// <summary>
    /// Automatically stop collection time, null means not to stop automatically
    /// </summary>
    public TimeSpan? AutoStopAfter { get; set; } = TimeSpan.FromMinutes(10);
}