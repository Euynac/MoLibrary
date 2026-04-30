using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Localization.Services;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Framework.UI.Localization;
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
        services.AddSingleton<EventBusProviderDiscoveryService>();
    }

    public override void ClaimDependencies()
    {
        // Depends on EventBus module
        DependsOnModule<ModuleEventBusGuide>().Register();

        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<EventBusResource>();

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
                .WithName(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:List:Name"))
                .WithTags(tagName)
                .WithSummary(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:List:Summary"))
                .WithDescription(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:List:Description"));

            // Get subscription details
            endpoints.MapGet("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.GetSubscriptionByIdAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Detail:Name"))
                .WithTags(tagName)
                .WithSummary(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Detail:Summary"))
                .WithDescription(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Detail:Description"));

            // Get statistics
            endpoints.MapGet("/eventbus-ui/statistics",
                async ([FromServices] EventBusMonitorService service) =>
                {
                    var result = await service.GetStatisticsAsync();
                    return result.GetResponse();
                })
                .WithName(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Statistics:Name"))
                .WithTags(tagName)
                .WithSummary(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Statistics:Summary"))
                .WithDescription(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Statistics:Description"));

            // Activate subscription
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/activate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.ActivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Activate:Name"))
                .WithTags(tagName)
                .WithSummary(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Activate:Summary"))
                .WithDescription(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Activate:Description"));

            // Deactivate subscription
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/deactivate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.DeactivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Deactivate:Name"))
                .WithTags(tagName)
                .WithSummary(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Deactivate:Summary"))
                .WithDescription(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Deactivate:Description"));

            // Remove subscription
            endpoints.MapDelete("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.UnsubscribeAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Remove:Name"))
                .WithTags(tagName)
                .WithSummary(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Remove:Summary"))
                .WithDescription(LocalizationManager.Get<EventBusResource>("Api:Subscriptions:Remove:Description"));
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
    /// Whether to disable the event bus monitoring page.
    /// </summary>
    public bool DisablePage { get; set; }

    /// <summary>
    /// Maximum number of recent messages retained by each active row-level test listener.
    /// The default is 20. Increase this when developers need a longer listener history during
    /// manual diagnostics; the limit is applied per active listener and older messages are
    /// discarded as new messages arrive.
    /// </summary>
    public int TestListenerMessageLimit { get; set; } = 20;
}
