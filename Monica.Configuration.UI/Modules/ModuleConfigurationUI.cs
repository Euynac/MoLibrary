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
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleConfigurationUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ConfigurationUI 模块
        /// </summary>
        public static ModuleConfigurationUIGuide AddConfigurationUI(Action<ModuleConfigurationUIOption>? action = null)
        {
            return new ModuleConfigurationUIGuide().Register(action);
        }
    }
}

/// <summary>
/// 配置管理UI模块
/// </summary>
public class ModuleConfigurationUI(ModuleConfigurationUIOption option)
    : MoModuleWithDependencies<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ConfigurationUI;
    }

    public override void ClaimDependencies()
    {
        // 依赖配置模块
        DependsOnModule<ModuleConfigurationGuide>().Register();
        DependsOnModule<ModuleRegisterCentreGuide>().Register();
        DependsOnModule<ModuleServiceInvocationGuide>().Register();
        
        if (!option.DisableConfigurationPage)
        {
            // 依赖差异对比模块
            DependsOnModule<ModuleDiffHighlightGuide>().Register();

            // 依赖UI核心模块并注册UI组件
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    // 注册面板配置页面
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
        // 注册统一配置UI服务
        services.AddScoped<ConfigurationUIService>();
        services.TryAddTransient<IMoConfigurationStores, MoConfigurationDefaultMemoryStore>();
        services.TryAddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
        services.TryAddSingleton<ConfigurationClientApiProvider>();
        
        if (GetOptions<ModuleRegisterCentreOption>().IsCentreServer)
        {
            // Dashboard mode: register centre API provider
            services.TryAddSingleton<ConfigurationCentreApiProvider>();
            services.TryAddSingleton<IMoConfigurationApi>(p =>
                p.GetRequiredService<ConfigurationCentreApiProvider>());

            // Register service invoker based on standalone mode
            if (GetOptions<ModuleRegisterCentreOption>().IsStandaloneMode)
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
        // Dashboard centre mode endpoints
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
/// 回滚请求模型
/// </summary>
public class RollbackRequest
{
    public required string Key { get; set; }
    public required string AppId { get; set; }
    public required string Version { get; set; }
}

/// <summary>
/// 配置管理UI模块配置指南
/// </summary>
public class ModuleConfigurationUIGuide : MoModuleGuide<ModuleConfigurationUI, ModuleConfigurationUIOption,
    ModuleConfigurationUIGuide>
{
    /// <summary>
    /// 配置自定义配置存储
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
/// 配置管理UI模块选项
/// </summary>
public class ModuleConfigurationUIOption : MoModuleOptionWithMinimalApi<ModuleConfigurationUI>
{
    /// <summary>
    /// 是否禁用配置管理页面
    /// </summary>
    public bool DisableConfigurationPage { get; set; } = false;

    /// <summary>
    /// 页面标题
    /// </summary>
    public string PageTitle { get; set; } = "配置管理";

    /// <summary>
    /// 是否启用实时更新
    /// </summary>
    public bool EnableRealTimeUpdates { get; set; } = true;

    /// <summary>
    /// 默认页面大小
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// 是否显示历史记录
    /// </summary>
    public bool ShowHistory { get; set; } = true;

    /// <summary>
    /// 历史记录保留天数
    /// </summary>
    public int HistoryRetentionDays { get; set; } = 180;

    /// <summary>
    /// 是否允许配置编辑
    /// </summary>
    public bool AllowEdit { get; set; } = true;

    /// <summary>
    /// 是否允许配置回滚
    /// </summary>
    public bool AllowRollback { get; set; } = true;
}