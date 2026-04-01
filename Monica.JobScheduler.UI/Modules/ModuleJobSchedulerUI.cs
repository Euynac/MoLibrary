using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.Pages;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJobSchedulerUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the JobSchedulerUI module
        /// </summary>
        public static ModuleJobSchedulerUIGuide AddJobSchedulerUI(Action<ModuleJobSchedulerUIOption>? action = null)
        {
            return new ModuleJobSchedulerUIGuide().Register(action);
        }
    }
}

/// <summary>
/// JobScheduler UI module implementation
/// Provides a job scheduling management interface based on Blazor
/// </summary>
[ModuleKey(EMoModuleKey.JobSchedulerUI)]
public class ModuleJobSchedulerUI(ModuleJobSchedulerUIOption option)
    : MoModule<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption, ModuleJobSchedulerUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register UI-only support helpers.
        services.AddSingleton<JobStateColorResolver>();
        services.AddSingleton<JobArgsJsonSchemaSupport>();
        services.AddSingleton<CronExpressionSupport>();
        // StackTraceParserService is now registered by ModuleUIStackTrace module
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<JobSchedulerResource>();

        // Depends on the backend JobScheduler module
        DependsOnModule<ModuleJobSchedulerGuide>().Register();

        // Depends on UIStackTrace module (for stack trace visualization)
        DependsOnModule<ModuleUIStackTraceGuide>().Register();

        // Depend on the UI core module and register the page
        if (!Option.DisableJobSchedulerPages)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    // Overview dashboard
                    p.RegisterLocalizedComponent<DashboardPage>(
                        DashboardPage.PAGE_URL,
                        "Pages:JobSchedulerDashboard:Title",
                        Icons.Material.Filled.Dashboard,
                        "Categories:TaskScheduling",
                        addToNav: true,
                        navOrder: 99,
                        navLinkMatch: NavLinkMatch.All);

                    // Real-time monitoring
                    p.RegisterLocalizedComponent<MonitorPage>(
                        MonitorPage.PAGE_URL,
                        "Pages:JobSchedulerMonitor:Title",
                        Icons.Material.Filled.Monitor,
                        "Categories:TaskScheduling",
                        addToNav: true,
                        navOrder: 100);

                    p.RegisterLocalizedComponent<JobDefinitionsPage>(
                        JobDefinitionsPage.PAGE_URL,
                        "Pages:JobDefinitions:Title",
                        Icons.Material.Filled.WorkOutline,
                        "Categories:TaskScheduling",
                        addToNav: true,
                        navOrder: 101);

                    p.RegisterLocalizedComponent<JobInstancesPage>(
                        JobInstancesPage.PAGE_URL,
                        "Pages:JobInstances:Title",
                        Icons.Material.Filled.PlaylistPlay,
                        "Categories:TaskScheduling",
                        addToNav: true,
                        navOrder: 102);

                    // Statistical analysis
                    p.RegisterLocalizedComponent<StatisticsPage>(
                        StatisticsPage.PAGE_URL,
                        "Pages:JobStatistics:Title",
                        Icons.Material.Filled.Analytics,
                        "Categories:TaskScheduling",
                        addToNav: true,
                        navOrder: 103);
                });
        }
    }
}

/// <summary>
/// JobScheduler UI module configuration guide
/// </summary>
public class ModuleJobSchedulerUIGuide
    : MoModuleGuide<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption, ModuleJobSchedulerUIGuide>
{
    // Configuration methods can be added later if needed
    // Currently, it can be configured directly through Mo.AddJobSchedulerUI(options => { ... })
}

/// <summary>
/// JobScheduler UI module configuration options
/// </summary>
public class ModuleJobSchedulerUIOption : MoModuleOption<ModuleJobSchedulerUI>
{
    /// <summary>
    /// Disable the JobScheduler UI page
    /// </summary>
    public bool DisableJobSchedulerPages { get; set; } = false;

    /// <summary>
    /// Health indicator time window (default 30 days)
    /// Configure statistics for health in the past x time
    /// </summary>
    public TimeSpan HealthMetricsWindow { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Number of recent failed instances shown in health metrics (default 5)
    /// Configuration displays the latest x failed instance records
    /// </summary>
    public int HealthMetricsFailedInstancesLimit { get; set; } = 5;

    /// <summary>
    /// Table default page size (default 20)
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Auto refresh interval (default 5 seconds)
    /// Set to a higher value (like 10 seconds) to reduce server load
    /// </summary>
    public TimeSpan AutoRefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Auto-refresh is enabled by default
    /// </summary>
    public bool EnableAutoRefreshByDefault { get; set; } = true;
}
