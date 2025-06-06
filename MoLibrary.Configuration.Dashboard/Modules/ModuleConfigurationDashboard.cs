using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Modules;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Modules;

public class ModuleConfigurationDashboard(ModuleConfigurationDashboardOption option)
    : MoModuleWithDependencies<ModuleConfigurationDashboard, ModuleConfigurationDashboardOption, ModuleConfigurationDashboardGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ConfigurationDashboard;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleRegisterCentreGuide>().Register();
        DependsOnModule<ModuleConfigurationGuide>().Register();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (option.ThisIsDashboard)
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = option.GetSwaggerGroupName(), Description = "配置中心" } };
                endpoints.MapGet(MoConfigurationConventions.DashboardCentreConfigHistory,
                    async ([FromQuery] string? key, [FromQuery] string? appid, [FromQuery] DateTime? start,
                        [FromQuery] DateTime? end, [FromServices] IMoConfigurationStores stores,
                        [FromServices] IMoUnitOfWorkManager manager) =>
                    {
                        using var uow = manager.Begin();
                        if (appid != null && key != null)
                        {
                            return (await stores.GetHistory(key, appid)).GetResponse();
                        }

                        if (start != null && end != null)
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


                endpoints.MapPost(MoConfigurationConventions.DashboardCentreConfigRollback,
                    async ([FromBody] Modules.RollbackRequest req, [FromServices] IMoConfigurationCentre centre,
                        [FromServices] IMoUnitOfWorkManager manager) =>
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

                endpoints.MapPost(MoConfigurationConventions.DashboardCentreConfigUpdate, async (DtoUpdateConfig req,
                    [FromServices] IMoConfigurationCentre centre, [FromServices] IMoUnitOfWorkManager manager) =>
                {
                    using var uow = manager.Begin();
                    var value = req.Value;
                    return (await centre.UpdateConfig(req)).GetResponse();
                }).WithName("更新指定配置").WithOpenApi(operation =>
                {
                    operation.Summary = "更新指定配置";
                    operation.Description = "更新指定配置";
                    operation.Tags = tagGroup;
                    return operation;
                });

                endpoints.MapGet(MoConfigurationConventions.DashboardCentreOptionItemStatus,
                    async ([FromQuery] string? appid, [FromQuery] string key,
                        [FromServices] IMoConfigurationCentre centre) =>
                    {
                        if ((await centre.GetSpecificOptionItemAsync(key, appid)).IsFailed(out var error, out var data))
                            return error.GetResponse();
                        return ((IServiceResponse) Res.Ok(data)).GetResponse();
                    }).WithName("获取指定配置状态").WithOpenApi(operation =>
                {
                    operation.Summary = "获取指定配置状态";
                    operation.Description = "获取指定配置状态";
                    operation.Tags = tagGroup;
                    return operation;
                });
                endpoints.MapGet(MoConfigurationConventions.DashboardCentreAllConfigStatus, async (
                    [FromQuery] string? mode,
                    [FromServices] IMoConfigurationCentre centre, [FromServices] IMoConfigurationDashboard dashboard) =>
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
        else
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag>
                    {new() {Name = option.GetSwaggerGroupName(), Description = "热配置面板相关内置接口"}};
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
}

file class RollbackRequest
{
    public required string Key { get; set; }
    public required string AppId { get; set; }
    public required string Version { get; set; }
}