using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Framework.UI.UIEventBus.Services;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleEventBusUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 EventBusUI 模块
        /// </summary>
        public static ModuleEventBusUIGuide AddEventBusUI(Action<ModuleEventBusUIOption>? action = null)
        {
            return new ModuleEventBusUIGuide().Register(action);
        }
    }
}

/// <summary>
/// 事件总线UI模块
/// </summary>
public class ModuleEventBusUI(ModuleEventBusUIOption option)
    : MoModuleWithDependencies<ModuleEventBusUI, ModuleEventBusUIOption, ModuleEventBusUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.EventBusUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册为Singleton以维护实时订阅状态
        services.AddSingleton<EventBusMonitorService>();

        // 注册测试服务，用于分布式事件总线测试
        services.AddSingleton<EventBusTestService>();

        // 注册 Provider 发现服务
        services.AddScoped<EventBusProviderDiscoveryService>();
    }

    public override void ClaimDependencies()
    {
        // 依赖EventBus模块
        DependsOnModule<ModuleEventBusGuide>().Register();

        // 注册UI页面
        if (!Option.DisableUIEventBusPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIEventBusPage>(
                    UIEventBusPage.PAGE_URL,
                    "Pages:EventBusMonitor:Title",
                    Icons.Material.Filled.Hub,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 40));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            // 获取所有订阅
            endpoints.MapGet("/eventbus-ui/subscriptions",
                async ([FromServices] EventBusMonitorService service) =>
                {
                    var result = await service.GetAllSubscriptionsAsync();
                    return result.GetResponse();
                })
                .WithName("获取所有订阅")
                .WithTags(tagName)
                .WithSummary("获取所有订阅")
                .WithDescription("获取所有EventBus订阅信息，包括本地和分布式订阅");

            // 获取订阅详情
            endpoints.MapGet("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.SubscriptionId(id);
                    var result = await service.GetSubscriptionByIdAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("获取订阅详情")
                .WithTags(tagName)
                .WithSummary("获取订阅详情")
                .WithDescription("根据订阅ID获取详细信息");

            // 获取统计信息
            endpoints.MapGet("/eventbus-ui/statistics",
                async ([FromServices] EventBusMonitorService service) =>
                {
                    var result = await service.GetStatisticsAsync();
                    return result.GetResponse();
                })
                .WithName("获取订阅统计")
                .WithTags(tagName)
                .WithSummary("获取订阅统计")
                .WithDescription("获取订阅的统计信息，包括总数、状态分布、范围分布等");

            // 激活订阅
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/activate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.SubscriptionId(id);
                    var result = await service.ActivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("激活订阅")
                .WithTags(tagName)
                .WithSummary("激活订阅")
                .WithDescription("激活处于Pending或Inactive状态的订阅");

            // 停用订阅
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/deactivate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.SubscriptionId(id);
                    var result = await service.DeactivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("停用订阅")
                .WithTags(tagName)
                .WithSummary("停用订阅")
                .WithDescription("停用活跃的订阅（不移除）");

            // 移除订阅
            endpoints.MapDelete("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.SubscriptionId(id);
                    var result = await service.UnsubscribeAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("移除订阅")
                .WithTags(tagName)
                .WithSummary("移除订阅")
                .WithDescription("永久移除订阅");
        });
    }
}

/// <summary>
/// EventBusUI模块向导
/// </summary>
public class ModuleEventBusUIGuide : MoModuleGuide<ModuleEventBusUI, ModuleEventBusUIOption, ModuleEventBusUIGuide>
{
}

/// <summary>
/// EventBusUI模块选项
/// </summary>
public class ModuleEventBusUIOption : MoModuleOptionWithMinimalApi<ModuleEventBusUI>
{
    /// <summary>
    /// 是否禁用事件总线监控页面
    /// </summary>
    public bool DisableUIEventBusPage { get; set; }
}
