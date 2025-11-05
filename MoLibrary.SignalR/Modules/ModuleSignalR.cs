using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.SignalR.Services;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalR(ModuleSignalROption option) : MoModule<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalR;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<MoSignalRManageService>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new()
                {
                    Name = option.GetApiGroupName(),
                    Description = "SignalR管理相关接口"
                }
            };

            // 获取SignalR所有Server端Hub信息
            endpoints.MapGet("/signalr/hubs",
                async ([FromServices] MoSignalRManageService service) =>
                {
                    return (await service.GetHubInfosAsync()).GetResponse();
                })
                .WithName("获取SignalR Hub信息")
                .WithOpenApi(operation =>
                {
                    operation.Summary = "获取SignalR所有Server端Hub信息";
                    operation.Description = "获取所有注册的SignalR Hub的详细信息，包括路由、方法和参数";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // 获取当前所有已连接的SignalR用户
            endpoints.MapGet("/signalr/connected-users",
                async ([FromServices] MoSignalRManageService service) =>
                {
                    return (await service.GetConnectedUsersAsync()).GetResponse();
                })
                .WithName("获取已连接用户")
                .WithOpenApi(operation =>
                {
                    operation.Summary = "获取当前所有已连接的SignalR用户";
                    operation.Description = "获取所有当前连接到SignalR的用户信息，包括连接ID、用户信息和Claims";
                    operation.Tags = tagGroup;
                    return operation;
                });
        });
    }
}