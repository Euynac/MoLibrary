using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Framework.UI.Pages;
using Monica.Framework.UI.UIEventBus.State;
using Monica.Framework.UI.UIEventBus.Support;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleEventBusUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the EventBusUI module
        /// </summary>
        public static ModuleEventBusUIGuide AddEventBusUI(Action<ModuleEventBusUIOption>? action = null)
        {
            return new ModuleEventBusUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Event bus UI module
/// </summary>
[ModuleKey(BuiltInModuleKey.EventBusUI)]
public class ModuleEventBusUI(ModuleEventBusUIOption option)
    : WebModuleBase<ModuleEventBusUI, ModuleEventBusUIOption, ModuleEventBusUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register as Singleton to maintain real-time subscription status
        services.AddSingleton<EventBusMonitorService>();

        // Register a test service for distributed event bus testing
        services.AddSingleton<EventBusTestService>();

        // Register the Provider Discovery Service
        services.AddScoped<EventBusProviderDiscoveryService>();
    }

    public override void ClaimDependencies()
    {
        // Depends on EventBus module
        DependsOnModule<ModuleEventBusGuide>().Register();

        // Registration UI page
        if (!Option.DisablePage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIEventBusMonitorPage>(
                    UIEventBusMonitorPage.PAGE_URL,
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

            // Get all subscriptions
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

            // Get subscription details
            endpoints.MapGet("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.GetSubscriptionByIdAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("获取订阅详情")
                .WithTags(tagName)
                .WithSummary("获取订阅详情")
                .WithDescription("根据订阅ID获取详细信息");

            // Get statistics
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

            // Activate subscription
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/activate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.ActivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("激活订阅")
                .WithTags(tagName)
                .WithSummary("激活订阅")
                .WithDescription("激活处于Pending或Inactive状态的订阅");

            // Deactivate subscription
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/deactivate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.DeactivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("停用订阅")
                .WithTags(tagName)
                .WithSummary("停用订阅")
                .WithDescription("停用活跃的订阅（不移除）");

            // Remove subscription
            endpoints.MapDelete("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
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
/// EventBusUI Module Wizard
/// </summary>
public class ModuleEventBusUIGuide : WebModuleGuide<ModuleEventBusUI, ModuleEventBusUIOption, ModuleEventBusUIGuide>
{
}

/// <summary>
/// EventBusUI module options
/// </summary>
public class ModuleEventBusUIOption : MinimalApiModuleOptions<ModuleEventBusUI>
{
    /// <summary>
    /// Whether to disable the event bus monitoring page
    /// </summary>
    public bool DisablePage { get; set; }
}
