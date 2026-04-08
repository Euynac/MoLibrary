using Monica.Configuration.UI.Pages;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleConfigurationUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the ConfigurationUI module
        /// </summary>
        public static ModuleConfigurationUIGuide AddConfigurationUI(Action<ModuleConfigurationUIOption>? action = null)
        {
            return new ModuleConfigurationUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Configuration management UI module
/// </summary>
[ModuleKey(BuiltInModuleKey.ConfigurationUI)]
public class ModuleConfigurationUI(ModuleConfigurationUIOption option)
    : ModuleBase<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>(option)
{

    public override void ClaimDependencies()
    {
        // Depend on configuration module
        DependsOnModule<ModuleConfigurationGuide>().Register();
        
        if (!option.DisableConfigurationPage)
        {
            // Dependency difference comparison module
            DependsOnModule<ModuleDiffHighlightGuide>().Register();

            // Depend on UI core module and register UI components
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    // Registration panel configuration page
                    registry.RegisterLocalizedComponent<UIConfigurationDashboardPage>(
                        UIConfigurationDashboardPage.PAGE_URL,
                        "Pages:ConfigurationDashboard:Title",
                        Icons.Material.Filled.Dashboard,
                        "Categories:Configuration",
                        addToNav: true,
                        navOrder: 10);
            });
        }
    }
}

/// <summary>
/// Configuration Management UI Module Configuration Guide
/// </summary>
public class ModuleConfigurationUIGuide : ModuleGuide<ModuleConfigurationUI, ModuleConfigurationUIOption,
    ModuleConfigurationUIGuide>
{
}

/// <summary>
/// Configure management UI module options
/// </summary>
public class ModuleConfigurationUIOption : MinimalApiModuleOptions<ModuleConfigurationUI>
{
    /// <summary>
    /// Whether to disable the configuration management page
    /// </summary>
    public bool DisableConfigurationPage { get; set; } = false;

    /// <summary>
    /// Page title
    /// </summary>
    public string PageTitle { get; set; } = "配置管理";

    /// <summary>
    /// Whether to enable real-time updates
    /// </summary>
    public bool EnableRealTimeUpdates { get; set; } = true;

    /// <summary>
    /// Default page size
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Whether to display history records
    /// </summary>
    public bool ShowHistory { get; set; } = true;

    /// <summary>
    /// Number of days to keep historical records
    /// </summary>
    public int HistoryRetentionDays { get; set; } = 180;

    /// <summary>
    /// Whether to allow configuration editing
    /// </summary>
    public bool AllowEdit { get; set; } = true;

    /// <summary>
    /// Whether to allow configuration rollback
    /// </summary>
    public bool AllowRollback { get; set; } = true;
}
