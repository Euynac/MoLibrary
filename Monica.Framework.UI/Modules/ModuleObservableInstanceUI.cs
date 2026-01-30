using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Modules;
using Monica.Framework.UI.UIObservableInstance.Services;
using Monica.Framework.UI.Pages;
using Monica.UI.Modules;
using MudBlazor;

namespace Monica.Framework.UI.Modules;

public static class ModuleObservableInstanceUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ObservableInstanceUI 模块
        /// </summary>
        public static ModuleObservableInstanceUIGuide AddObservableInstanceUI(Action<ModuleObservableInstanceUIOption>? action = null)
        {
            return new ModuleObservableInstanceUIGuide().Register(action);
        }
    }
}

/// <summary>
/// ObservableInstance UI module
/// </summary>
public class ModuleObservableInstanceUI(ModuleObservableInstanceUIOption option)
    : MoModuleWithDependencies<ModuleObservableInstanceUI, ModuleObservableInstanceUIOption, ModuleObservableInstanceUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ObservableInstanceUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register as Singleton to maintain consistent state
        services.AddSingleton<ObservableInstanceMonitorService>();
    }

    public override void ClaimDependencies()
    {
        // Depend on ObservableInstance module
        DependsOnModule<ModuleObservableInstanceGuide>().Register();

        // Register UI page
        if (!Option.DisableUIObservableInstancePage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UIObservableInstancePage>(
                    UIObservableInstancePage.PAGE_URL,
                    "Observable Instance",
                    Icons.Material.Filled.Inventory,
                    "调试",
                    addToNav: true,
                    navOrder: 50));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            // Get all instances
            endpoints.MapGet("/observable-instance-ui/instances",
                async ([FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetAllInstancesAsync();
                    return result.GetResponse();
                })
                .WithName("获取所有Observable实例")
                .WithTags(tagName)
                .WithSummary("获取所有Observable实例")
                .WithDescription("获取所有已注册的Observable Instance信息");

            // Get instance by ID
            endpoints.MapGet("/observable-instance-ui/instances/{id}",
                async ([FromRoute] string id,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstanceByIdAsync(id);
                    return result.GetResponse();
                })
                .WithName("获取Observable实例详情")
                .WithTags(tagName)
                .WithSummary("获取Observable实例详情")
                .WithDescription("根据实例ID获取详细信息");

            // Get instances with exceptions
            endpoints.MapGet("/observable-instance-ui/instances/with-exceptions",
                async ([FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstancesWithExceptionsAsync();
                    return result.GetResponse();
                })
                .WithName("获取有异常的实例")
                .WithTags(tagName)
                .WithSummary("获取有异常的实例")
                .WithDescription("获取所有包含异常记录的实例");

            // Get statistics
            endpoints.MapGet("/observable-instance-ui/statistics",
                async ([FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetStatisticsAsync();
                    return result.GetResponse();
                })
                .WithName("获取Observable实例统计")
                .WithTags(tagName)
                .WithSummary("获取Observable实例统计")
                .WithDescription("获取实例的统计信息，包括总数、异常分布等");

            // Get instance history
            endpoints.MapGet("/observable-instance-ui/instances/{id}/history",
                async ([FromRoute] string id,
                       [FromQuery] int? limit,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstanceHistoryAsync(id, limit);
                    return result.GetResponse();
                })
                .WithName("获取实例历史")
                .WithTags(tagName)
                .WithSummary("获取实例历史")
                .WithDescription("获取实例的状态变更历史");

            // Get instance exceptions
            endpoints.MapGet("/observable-instance-ui/instances/{id}/exceptions",
                async ([FromRoute] string id,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstanceExceptionsAsync(id);
                    return result.GetResponse();
                })
                .WithName("获取实例异常")
                .WithTags(tagName)
                .WithSummary("获取实例异常")
                .WithDescription("获取实例的所有异常记录");

            // Clear instance history
            endpoints.MapPost("/observable-instance-ui/instances/{id}/clear-history",
                async ([FromRoute] string id,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.ClearInstanceHistoryAsync(id);
                    return result.GetResponse();
                })
                .WithName("清除实例历史")
                .WithTags(tagName)
                .WithSummary("清除实例历史")
                .WithDescription("清除指定实例的状态历史记录");
        });
    }
}

/// <summary>
/// ObservableInstanceUI module guide
/// </summary>
public class ModuleObservableInstanceUIGuide : MoModuleGuide<ModuleObservableInstanceUI, ModuleObservableInstanceUIOption, ModuleObservableInstanceUIGuide>
{
}

/// <summary>
/// ObservableInstanceUI module options
/// </summary>
public class ModuleObservableInstanceUIOption : MoModuleOptionWithMinimalApi<ModuleObservableInstanceUI>
{
    /// <summary>
    /// Whether to disable Observable Instance monitoring page
    /// </summary>
    public bool DisableUIObservableInstancePage { get; set; }
}
