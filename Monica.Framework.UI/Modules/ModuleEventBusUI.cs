using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Monica.Core;
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
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the EventBusUI module
        /// </summary>
        public ModuleEventBusUIGuide AddEventBusUI(Action<ModuleEventBusUIOption>? action = null)
        {
            return builder.AddModule<ModuleEventBusUI, ModuleEventBusUIOption, ModuleEventBusUIGuide>(action);
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
        var localizer = app.ApplicationServices.GetRequiredService<IStringLocalizer<EventBusResource>>();

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
                .WithName(localizer["Api:Subscriptions:List:Name"].Value)
                .WithTags(tagName)
                .WithSummary(localizer["Api:Subscriptions:List:Summary"].Value)
                .WithDescription(localizer["Api:Subscriptions:List:Description"].Value);

            // Get subscription details
            endpoints.MapGet("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.GetSubscriptionByIdAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(localizer["Api:Subscriptions:Detail:Name"].Value)
                .WithTags(tagName)
                .WithSummary(localizer["Api:Subscriptions:Detail:Summary"].Value)
                .WithDescription(localizer["Api:Subscriptions:Detail:Description"].Value);

            // Get statistics
            endpoints.MapGet("/eventbus-ui/statistics",
                async ([FromServices] EventBusMonitorService service) =>
                {
                    var result = await service.GetStatisticsAsync();
                    return result.GetResponse();
                })
                .WithName(localizer["Api:Subscriptions:Statistics:Name"].Value)
                .WithTags(tagName)
                .WithSummary(localizer["Api:Subscriptions:Statistics:Summary"].Value)
                .WithDescription(localizer["Api:Subscriptions:Statistics:Description"].Value);

            // Activate subscription
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/activate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.ActivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(localizer["Api:Subscriptions:Activate:Name"].Value)
                .WithTags(tagName)
                .WithSummary(localizer["Api:Subscriptions:Activate:Summary"].Value)
                .WithDescription(localizer["Api:Subscriptions:Activate:Description"].Value);

            // Deactivate subscription
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/deactivate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.DeactivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(localizer["Api:Subscriptions:Deactivate:Name"].Value)
                .WithTags(tagName)
                .WithSummary(localizer["Api:Subscriptions:Deactivate:Summary"].Value)
                .WithDescription(localizer["Api:Subscriptions:Deactivate:Description"].Value);

            // Remove subscription
            endpoints.MapDelete("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new EventBus.Models.EventSubscriptionId(id);
                    var result = await service.UnsubscribeAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName(localizer["Api:Subscriptions:Remove:Name"].Value)
                .WithTags(tagName)
                .WithSummary(localizer["Api:Subscriptions:Remove:Summary"].Value)
                .WithDescription(localizer["Api:Subscriptions:Remove:Description"].Value);
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
