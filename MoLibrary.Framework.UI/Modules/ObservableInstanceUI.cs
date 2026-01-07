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
using MoLibrary.Framework.UI.UIObservableInstance.Services;
using MoLibrary.Framework.UI.Pages;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.Framework.UI.Modules;

/// <summary>
/// ObservableInstanceUI module builder extensions
/// </summary>
public static class ModuleObservableInstanceUIBuilderExtensions
{
    public static ModuleObservableInstanceUIGuide ConfigModuleObservableInstanceUI(this WebApplicationBuilder builder,
        Action<ModuleObservableInstanceUIOption>? action = null)
    {
        return new ModuleObservableInstanceUIGuide().Register(action);
    }
}

/// <summary>
/// ObservableInstance UI module
/// </summary>
public class ModuleObservableInstanceUI(ModuleObservableInstanceUIOption option)
    : MoModuleWithDependencies<ModuleObservableInstanceUI, ModuleObservableInstanceUIOption, ModuleObservableInstanceUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ObservableInstanceUI;
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
                    "系统管理",
                    addToNav: true,
                    navOrder: 140));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = Option.GetApiGroupName(), Description = "Observable Instance 监控接口" }
            };

            // Get all instances
            endpoints.MapGet("/observable-instance-ui/instances",
                async ([FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetAllInstancesAsync();
                    return result.GetResponse();
                })
                .WithName("获取所有Observable实例").WithOpenApi(operation =>
                {
                    operation.Summary = "获取所有Observable实例";
                    operation.Description = "获取所有已注册的Observable Instance信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // Get instance by ID
            endpoints.MapGet("/observable-instance-ui/instances/{id}",
                async ([FromRoute] string id,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstanceByIdAsync(id);
                    return result.GetResponse();
                })
                .WithName("获取Observable实例详情").WithOpenApi(operation =>
                {
                    operation.Summary = "获取Observable实例详情";
                    operation.Description = "根据实例ID获取详细信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // Get instances with exceptions
            endpoints.MapGet("/observable-instance-ui/instances/with-exceptions",
                async ([FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstancesWithExceptionsAsync();
                    return result.GetResponse();
                })
                .WithName("获取有异常的实例").WithOpenApi(operation =>
                {
                    operation.Summary = "获取有异常的实例";
                    operation.Description = "获取所有包含异常记录的实例";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // Get statistics
            endpoints.MapGet("/observable-instance-ui/statistics",
                async ([FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetStatisticsAsync();
                    return result.GetResponse();
                })
                .WithName("获取Observable实例统计").WithOpenApi(operation =>
                {
                    operation.Summary = "获取Observable实例统计";
                    operation.Description = "获取实例的统计信息，包括总数、异常分布等";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // Get instance history
            endpoints.MapGet("/observable-instance-ui/instances/{id}/history",
                async ([FromRoute] string id,
                       [FromQuery] int? limit,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstanceHistoryAsync(id, limit);
                    return result.GetResponse();
                })
                .WithName("获取实例历史").WithOpenApi(operation =>
                {
                    operation.Summary = "获取实例历史";
                    operation.Description = "获取实例的状态变更历史";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // Get instance exceptions
            endpoints.MapGet("/observable-instance-ui/instances/{id}/exceptions",
                async ([FromRoute] string id,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.GetInstanceExceptionsAsync(id);
                    return result.GetResponse();
                })
                .WithName("获取实例异常").WithOpenApi(operation =>
                {
                    operation.Summary = "获取实例异常";
                    operation.Description = "获取实例的所有异常记录";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // Clear instance history
            endpoints.MapPost("/observable-instance-ui/instances/{id}/clear-history",
                async ([FromRoute] string id,
                       [FromServices] ObservableInstanceMonitorService service) =>
                {
                    var result = await service.ClearInstanceHistoryAsync(id);
                    return result.GetResponse();
                })
                .WithName("清除实例历史").WithOpenApi(operation =>
                {
                    operation.Summary = "清除实例历史";
                    operation.Description = "清除指定实例的状态历史记录";
                    operation.Tags = tagGroup;
                    return operation;
                });
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
public class ModuleObservableInstanceUIOption : MoModuleControllerOption<ModuleObservableInstanceUI>
{
    /// <summary>
    /// Whether to disable Observable Instance monitoring page
    /// </summary>
    public bool DisableUIObservableInstancePage { get; set; }
}
