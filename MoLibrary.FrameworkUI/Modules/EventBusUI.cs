using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.EventBus.Modules;
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.FrameworkUI.UIEventBus.Services;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;

/// <summary>
/// EventBusUI模块构建器扩展
/// </summary>
public static class ModuleEventBusUIBuilderExtensions
{
    public static ModuleEventBusUIGuide ConfigModuleEventBusUI(this WebApplicationBuilder builder,
        Action<ModuleEventBusUIOption>? action = null)
    {
        return new ModuleEventBusUIGuide().Register(action);
    }
}

/// <summary>
/// 事件总线UI模块
/// </summary>
public class ModuleEventBusUI(ModuleEventBusUIOption option)
    : MoModuleWithDependencies<ModuleEventBusUI, ModuleEventBusUIOption, ModuleEventBusUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.EventBusUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册为Singleton以维护实时订阅状态
        services.AddSingleton<EventBusMonitorService>();

        // 注册测试服务，用于分布式事件总线测试
        services.AddSingleton<EventBusTestService>();
    }

    public override void ClaimDependencies()
    {
        // 依赖EventBus模块
        DependsOnModule<ModuleEventBusGuide>().Register();

        // 注册UI页面
        if (!Option.DisableUIEventBusPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UIEventBusPage>(
                    UIEventBusPage.PAGE_URL,
                    "事件总线监控",
                    Icons.Material.Filled.Hub,
                    "系统管理",
                    addToNav: true,
                    navOrder: 150));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (Option.DisableAPIEndpoints)
        {
            return;
        }

        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = Option.GetApiGroupName(), Description = "事件总线监控接口" }
            };

            // 获取所有订阅
            endpoints.MapGet("/eventbus-ui/subscriptions",
                async ([FromServices] EventBusMonitorService service) =>
                {
                    var result = await service.GetAllSubscriptionsAsync();
                    return result.GetResponse();
                })
                .WithName("获取所有订阅").WithOpenApi(operation =>
                {
                    operation.Summary = "获取所有订阅";
                    operation.Description = "获取所有EventBus订阅信息，包括本地和分布式订阅";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // 获取订阅详情
            endpoints.MapGet("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new MoLibrary.EventBus.Models.SubscriptionId(id);
                    var result = await service.GetSubscriptionByIdAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("获取订阅详情").WithOpenApi(operation =>
                {
                    operation.Summary = "获取订阅详情";
                    operation.Description = "根据订阅ID获取详细信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // 获取统计信息
            endpoints.MapGet("/eventbus-ui/statistics",
                async ([FromServices] EventBusMonitorService service) =>
                {
                    var result = await service.GetStatisticsAsync();
                    return result.GetResponse();
                })
                .WithName("获取订阅统计").WithOpenApi(operation =>
                {
                    operation.Summary = "获取订阅统计";
                    operation.Description = "获取订阅的统计信息，包括总数、状态分布、范围分布等";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // 激活订阅
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/activate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new MoLibrary.EventBus.Models.SubscriptionId(id);
                    var result = await service.ActivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("激活订阅").WithOpenApi(operation =>
                {
                    operation.Summary = "激活订阅";
                    operation.Description = "激活处于Pending或Inactive状态的订阅";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // 停用订阅
            endpoints.MapPost("/eventbus-ui/subscriptions/{id:guid}/deactivate",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new MoLibrary.EventBus.Models.SubscriptionId(id);
                    var result = await service.DeactivateSubscriptionAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("停用订阅").WithOpenApi(operation =>
                {
                    operation.Summary = "停用订阅";
                    operation.Description = "停用活跃的订阅（不移除）";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // 移除订阅
            endpoints.MapDelete("/eventbus-ui/subscriptions/{id:guid}",
                async ([FromRoute] Guid id,
                       [FromServices] EventBusMonitorService service) =>
                {
                    var subscriptionId = new MoLibrary.EventBus.Models.SubscriptionId(id);
                    var result = await service.UnsubscribeAsync(subscriptionId);
                    return result.GetResponse();
                })
                .WithName("移除订阅").WithOpenApi(operation =>
                {
                    operation.Summary = "移除订阅";
                    operation.Description = "永久移除订阅";
                    operation.Tags = tagGroup;
                    return operation;
                });
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
public class ModuleEventBusUIOption : MoModuleControllerOption<ModuleEventBusUI>
{
    /// <summary>
    /// 是否禁用事件总线监控页面
    /// </summary>
    public bool DisableUIEventBusPage { get; set; }

    /// <summary>
    /// 是否禁用API端点
    /// </summary>
    public bool DisableAPIEndpoints { get; set; }
}
