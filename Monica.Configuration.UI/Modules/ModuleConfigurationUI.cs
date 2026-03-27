using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Configuration;
using Monica.Configuration.UI.Implements;
using Monica.Configuration.UI.Interfaces;
using Monica.Configuration.UI.Model;
using Monica.Configuration.UI.Pages;
using Monica.Configuration.UI.Services;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
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
[ModuleKey(EMoModuleKey.ConfigurationUI)]
public class ModuleConfigurationUI(ModuleConfigurationUIOption option)
    : MoModule<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>(option)
{

    public override void ClaimDependencies()
    {
        // Depend on configuration module
        DependsOnModule<ModuleConfigurationGuide>().Register();
        DependsOnModule<ModuleServiceDiscoveryGuide>().Register();
        DependsOnModule<ModuleServiceInvocationGuide>().Register();
        
        if (!option.DisableConfigurationPage)
        {
            // Dependency difference comparison module
            DependsOnModule<ModuleDiffHighlightGuide>().Register();

            // Depend on UI core module and register UI components
            DependsOnModule<ModuleUICoreGuide>().Register()
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

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the unified configuration UI service
        services.AddScoped<ConfigurationUIService>();
        services.TryAddTransient<IMoConfigurationStores, MoConfigurationDefaultMemoryStore>();
        services.TryAddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
        services.TryAddSingleton<ConfigurationClientApiProvider>();
        
        if (GetOptions<ModuleServiceDiscoveryOption>().IsRegistryServer)
        {
            // Dashboard mode: register the configuration API provider.
            services.TryAddSingleton<ConfigurationCentreApiProvider>();
            services.TryAddSingleton<IMoConfigurationApi>(p =>
                p.GetRequiredService<ConfigurationCentreApiProvider>());

            // Register service invoker based on standalone mode
            if (GetOptions<ModuleServiceDiscoveryOption>().IsStandaloneMode)
            {
                services.TryAddSingleton<IConfigurationCentreServiceInvoker,
                    ConfigurationCentreServiceInvokerStandaloneProvider>();
            }
            else
            {
                services.TryAddSingleton<IConfigurationCentreServiceInvoker,
                    ConfigurationCentreServiceInvokerDistributedProvider>();
            }
        }
        else
        {
            // Client mode: register client API provider
            services.TryAddSingleton<IMoConfigurationApi>(p =>
                p.GetRequiredService<ConfigurationClientApiProvider>());
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        // Dashboard mode endpoints.
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapGet(MoConfigurationConventions.DashboardConfigHistory,
                    async ([FromQuery] string? key, [FromQuery] string? appid, [FromQuery] DateTime? start,
                        [FromQuery] DateTime? end, [FromServices] ConfigurationUIService uiService) =>
                    {
                        return (await uiService.GetConfigHistoryAsync(key, appid, start, end)).GetResponse();
                    })
                .WithName("获取配置类历史")
                .WithTags(tagName)
                .WithSummary("获取配置类历史")
                .WithDescription("获取配置类历史");

            endpoints.MapPost(MoConfigurationConventions.DashboardConfigRollback,
                    async ([FromBody] RollbackRequest req, [FromServices] ConfigurationUIService uiService) =>
                    {
                        return (await uiService.RollbackConfigAsync(req.Key, req.AppId, req.Version)).GetResponse();
                    })
                .WithName("回滚配置类")
                .WithTags(tagName)
                .WithSummary("回滚配置类")
                .WithDescription("回滚配置类");

            endpoints.MapPost(MoConfigurationConventions.DashboardConfigUpdate, async (DtoUpdateConfig req,
                    [FromServices] ConfigurationUIService uiService) =>
                {
                    return (await uiService.UpdateConfigAsync(req)).GetResponse();
                })
                .WithName("更新指定配置")
                .WithTags(tagName)
                .WithSummary("更新指定配置")
                .WithDescription("更新指定配置");

            endpoints.MapGet(MoConfigurationConventions.DashboardOptionItemStatus,
                    async ([FromQuery] string? appid, [FromQuery] string key,
                        [FromServices] ConfigurationUIService uiService) =>
                    {
                        return (await uiService.GetOptionItemAsync(appid, key)).GetResponse();
                    })
                .WithName("获取指定配置状态")
                .WithTags(tagName)
                .WithSummary("获取指定配置状态")
                .WithDescription("获取指定配置状态");

            endpoints.MapGet(MoConfigurationConventions.DashboardAllConfigStatus, async (
                    [FromServices] ConfigurationUIService uiService,
                    [FromQuery] string? mode,
                    [FromQuery] bool onlyCurDomain = false) =>
                {
                    return (await uiService.GetConfigsAsync(mode, onlyCurDomain)).GetResponse();
                })
                .WithName("获取所有微服务配置状态")
                .WithTags(tagName)
                .WithSummary("获取所有微服务配置状态")
                .WithDescription("获取所有微服务配置状态");
        });
    }
}

/// <summary>
/// Rollback request model
/// </summary>
public class RollbackRequest
{
    public required string Key { get; set; }
    public required string AppId { get; set; }
    public required string Version { get; set; }
}

/// <summary>
/// Configuration Management UI Module Configuration Guide
/// </summary>
public class ModuleConfigurationUIGuide : MoModuleGuide<ModuleConfigurationUI, ModuleConfigurationUIOption,
    ModuleConfigurationUIGuide>
{
    /// <summary>
    /// Configure a custom configuration store
    /// </summary>
    public ModuleConfigurationUIGuide ConfigCustomStore<TStore>()
        where TStore : class, IMoConfigurationStores
    {
        ConfigureServices(context => { context.Services.AddTransient<IMoConfigurationStores, TStore>(); },
            EMoModuleOrder.PreConfig);
        return this;
    }
}

/// <summary>
/// Configure management UI module options
/// </summary>
public class ModuleConfigurationUIOption : MoModuleOptionWithMinimalApi<ModuleConfigurationUI>
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
