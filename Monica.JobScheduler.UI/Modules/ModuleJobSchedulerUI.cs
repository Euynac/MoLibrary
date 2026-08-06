using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.Pages;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJobSchedulerUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the JobSchedulerUI module
        /// </summary>
        public ModuleRegistration<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption> AddJobSchedulerUI(
            Action<ModuleJobSchedulerUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption>(action);
            registration.Require<ModuleStackTraceUI, ModuleStackTraceUIOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<JobSchedulerResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedPage<DashboardPage, JobSchedulerResource>(
                        DashboardPage.PAGE_URL,
                        "Pages:JobSchedulerDashboard:Title",
                        Icons.Material.Filled.Dashboard,
                        BuiltInNavigationCategoryIds.TaskScheduling,
                        addToNav: true,
                        navOrder: 99);
                    registry.RegisterLocalizedPage<MonitorPage, JobSchedulerResource>(
                        MonitorPage.PAGE_URL,
                        "Pages:JobSchedulerMonitor:Title",
                        Icons.Material.Filled.Monitor,
                        BuiltInNavigationCategoryIds.TaskScheduling,
                        addToNav: true,
                        navOrder: 100);
                    registry.RegisterLocalizedPage<JobDefinitionsPage, JobSchedulerResource>(
                        JobDefinitionsPage.PAGE_URL,
                        "Pages:JobDefinitions:Title",
                        Icons.Material.Filled.WorkOutline,
                        BuiltInNavigationCategoryIds.TaskScheduling,
                        addToNav: true,
                        navOrder: 101);
                    registry.RegisterLocalizedPage<JobInstancesPage, JobSchedulerResource>(
                        JobInstancesPage.PAGE_URL,
                        "Pages:JobInstances:Title",
                        Icons.Material.Filled.PlaylistPlay,
                        BuiltInNavigationCategoryIds.TaskScheduling,
                        addToNav: true,
                        navOrder: 102);
                    registry.RegisterLocalizedPage<StatisticsPage, JobSchedulerResource>(
                        StatisticsPage.PAGE_URL,
                        "Pages:JobStatistics:Title",
                        Icons.Material.Filled.Analytics,
                        BuiltInNavigationCategoryIds.TaskScheduling,
                        addToNav: true,
                        navOrder: 103);
                });
            return registration;
        }
    }
}

/// <summary>
/// JobScheduler UI module implementation
/// Provides a job scheduling management interface based on Blazor
/// </summary>
public class ModuleJobSchedulerUI : MonicaModule<ModuleJobSchedulerUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleJobScheduler, ModuleJobSchedulerOption>();
        module.Require<ModuleShellUI, ModuleShellUIOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleJobSchedulerUIOption> context)
    {
        var services = context.Services;
        // Register UI-only support helpers.
        services.AddScoped<JobStateColorResolver>();
        services.AddSingleton<JobArgsJsonSchemaSupport>();
        services.AddSingleton<CronExpressionSupport>();
        // StackTraceParser is now registered by ModuleStackTraceUI.
    }

}

/// <summary>
/// JobScheduler UI module configuration options
/// </summary>
public class ModuleJobSchedulerUIOption : ModuleOptions<ModuleJobSchedulerUI>
{
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
