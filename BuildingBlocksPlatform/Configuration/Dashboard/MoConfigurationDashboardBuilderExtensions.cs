using BuildingBlocksPlatform.Configuration.Dashboard.Interfaces;
using BuildingBlocksPlatform.Configuration.Dashboard.Model;
using BuildingBlocksPlatform.Core.RegisterCentre;
using BuildingBlocksPlatform.Extensions;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;

namespace BuildingBlocksPlatform.Configuration.Dashboard;

public static class MoConfigurationDashboardBuilderExtensions
{
    /// <summary>
    /// 注册MoConfigurationDashboard
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddMoConfigurationDashboard(this IServiceCollection services)
    {
        return AddMoConfigurationDashboard<DefaultArrangeDashboard>(services);
    }
    /// <summary>
    /// 注册MoConfigurationDashboard
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddMoConfigurationDashboard<TDashboard>(this IServiceCollection services)
        where TDashboard : class, IMoConfigurationDashboard
    {
        services.AddSingleton<IMoConfigurationDashboard, TDashboard>();
        services.AddSingleton<MemoryProviderForConfigCentre>();
        services.AddSingleton(p => ((IRegisterCentreServer?) p.GetService(typeof(MemoryProviderForConfigCentre)))!);
        services.AddMoRegisterCentre();
        services.AddSingleton(p => ((IMoConfigurationCentre?) p.GetService(typeof(MemoryProviderForConfigCentre)))!);
        MoConfigurationManager.Setting.ThisIsDashboard = true;
        services.AddSingleton<IMoConfigurationStores, MoConfigurationDefaultStore>();
        services.AddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
        return services;
    }


    public static void UseEndpointsMoConfigurationDashboard(this IApplicationBuilder app, string groupName = "配置中心")
    {
        app.UseEndpointsMoRegisterCentre();
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = groupName, Description = "配置中心" } };
            endpoints.MapGet(MoConfigurationConventions.DashboardCentreConfigHistory, 
                async ([FromQuery] string? key, [FromQuery] string? appid, [FromQuery] DateTime? start,
                [FromQuery] DateTime? end, [FromServices] IMoConfigurationStores stores) =>
            {
                if (appid != null && key != null)
                {
                    return (await stores.GetHistory(key, appid)).GetResponse();
                }
                if(start != null && end != null)
                {
                    return (await stores.GetHistory(start.Value, end.Value)).GetResponse();
                }

                return (await stores.GetHistory(DateTime.Now.Subtract(TimeSpan.FromDays(180)), DateTime.Now))
                    .GetResponse();
            }).WithName("获取配置类历史").WithOpenApi(operation =>
            {
                operation.Summary = "获取配置类历史";
                operation.Description = "获取配置类历史";
                operation.Tags = tagGroup;
                return operation;
            });


            endpoints.MapPost(MoConfigurationConventions.DashboardCentreConfigRollback, async ([FromBody] RollbackRequest req, [FromServices] IMoConfigurationCentre centre, [FromServices] IMoUnitOfWorkManager manager) =>
            {
                using var uow = manager.Begin();
                return (await centre.RollbackConfig(req.Key, req.AppId, req.Version)).GetResponse();

            }).WithName("回滚配置类").WithOpenApi(operation =>
            {
                operation.Summary = "回滚配置类";
                operation.Description = "回滚配置类";
                operation.Tags = tagGroup;
                return operation;
            });

            endpoints.MapPost(MoConfigurationConventions.DashboardCentreConfigUpdate, async (DtoUpdateConfig req, [FromServices] IMoConfigurationCentre modifier, [FromServices] IMoUnitOfWorkManager manager) =>
            {
                using var uow = manager.Begin();
                var value = req.Value;
                return (await modifier.UpdateConfig(req)).GetResponse();
            }).WithName("更新指定配置").WithOpenApi(operation =>
            {
                operation.Summary = "更新指定配置";
                operation.Description = "更新指定配置";
                operation.Tags = tagGroup;
                return operation;
            });


            endpoints.MapGet(MoConfigurationConventions.DashboardCentreConfigStatus, async ([FromQuery] string? mode, [FromServices] IMoConfigurationCentre centre, [FromServices] IMoConfigurationDashboard dashboard) =>
            {
                if ((await centre.GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data))
                    return error.GetResponse();

                if ((await dashboard.DashboardDisplayMode(data, mode)).IsFailed(out error, out var arranged))
                    return error.GetResponse();
                return ((IServiceResponse) Res.Ok(arranged)).GetResponse();
            }).WithName("获取所有微服务配置状态").WithOpenApi(operation =>
            {
                operation.Summary = "获取所有微服务配置状态";
                operation.Description = "获取所有微服务配置状态";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
    public static void AddMoConfigurationDashboardStore<TStore>(this IServiceCollection services)
        where TStore : class, IMoConfigurationStores
    {
        if (!MoConfigurationManager.Setting.ThisIsDashboard)
        {
            throw new InvalidOperationException("非面板服务无需注册面板仓储接口");
        }

        services.Replace(ServiceDescriptor.Transient<IMoConfigurationStores, TStore>());
    }

    public static void AddMoConfigurationDashboardClient<TServer, TClient>(
        this IServiceCollection services, Action<MoRegisterCentreSetting>? action = null)
        where TServer : class, IRegisterCentreServerConnector
        where TClient : class, IRegisterCentreClient
    {
        if (MoConfigurationManager.Setting.ThisIsDashboard) return;
        services.AddMoRegisterCentreClient<TServer, TClient>(action);
        services.AddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
    }

    public static void UseEndpointsMoConfigurationDashboardClient(this IApplicationBuilder app, string? groupName = "MoConfiguration")
    {
        if (MoConfigurationManager.Setting.ThisIsDashboard) return;
        app.UseEndpointsMoRegisterCentreClient();
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = groupName, Description = "热配置面板相关内置接口" } };
            endpoints.MapPost(MoConfigurationConventions.DashboardClientConfigUpdate,
                async (DtoUpdateConfig req, [FromServices] IMoConfigurationModifier modifier) =>
                {
                    var value = req.Value;

                    if ((await modifier.IsOptionExist(req.Key)).IsOk(out var option))
                    {
                        var res = await modifier.UpdateOption(option, value);
                        return res.GetResponse();
                    }

                    if ((await modifier.IsConfigExist(req.Key)).IsOk(out var config))
                    {
                        var res = await modifier.UpdateConfig(config, value);
                        return res.GetResponse();
                    }

                    return Res.Fail($"更新失败，找不到Key为{req.Key}的配置").GetResponse();
                }).WithName("配置中心更新指定配置").WithOpenApi(operation =>
            {
                operation.Summary = "更新指定配置";
                operation.Description = "更新指定配置";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}
file class RollbackRequest
{
    public required string Key { get; set; }
    public required string AppId { get; set; }
    public required string Version { get; set; }
}