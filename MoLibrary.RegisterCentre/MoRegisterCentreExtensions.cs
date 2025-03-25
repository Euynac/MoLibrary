using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre;

public static class MoRegisterCentreExtensions
{
 
    /// <summary>
    /// 注册MoRegisterCentre
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static void AddMoRegisterCentre(this IServiceCollection services)
    {
        if (MoRegisterCentreManager.HasSetServerOrClient) return;
        var setting = new MoRegisterCentreSetting
        {
            ThisIsCentreServer = true
        };
        MoRegisterCentreManager.Setting = setting;

        services.TryAddSingleton<IRegisterCentreServer, MemoryProviderForRegisterCentre>();
        services.TryAddSingleton<IRegisterCentreClientConnector, DaprHttpForConnectClient>();
    }

    /// <summary>
    /// 配置MoRegisterCentre Endpoints中间件
    /// </summary>
    /// <param name="app"></param>
    public static void UseMoRegisterCentre(this IApplicationBuilder app)
    {
        if (MoRegisterCentreManager.Setting.ThisIsCentreClient) return;
    }


    /// <summary>
    /// 配置MoRegisterCentre Endpoints中间件
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoRegisterCentre(this IApplicationBuilder app, string? groupName = "注册中心")
    {
        if (MoRegisterCentreManager.Setting.ThisIsCentreClient) return;
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = groupName, Description = "注册中心" } };
            endpoints.MapPost(MoRegisterCentreConventions.ServerCentreRegister, async (RegisterServiceStatus req, [FromServices] IRegisterCentreServer centre) =>
            {
                if ((await centre.Register(req)).IsFailed(out var error)) return error;
                return Res.Ok("注册成功");
            }).WithName("微服务注册热配置中心").WithOpenApi(operation =>
            {
                operation.Summary = "微服务注册配置中心";
                operation.Description = "微服务注册配置中心";
                operation.Tags = tagGroup;
                return operation;
            });

            endpoints.MapGet(MoRegisterCentreConventions.ServerCentreGetServicesStatus, async ([FromServices] IRegisterCentreServer centre) =>
            {
                if ((await centre.GetServicesStatus()).IsFailed(out var error, out var data))
                    return error.GetResponse();
                return Res.Create(data, ResponseCode.Ok).GetResponse();
            }).WithName("获取所有微服务状态").WithOpenApi(operation =>
            {
                operation.Summary = "获取所有微服务状态";
                operation.Description = "获取所有微服务状态";
                operation.Tags = tagGroup;
                return operation;
            });


            endpoints.MapGet(MoRegisterCentreConventions.ServerCentreUnregisterAll, async ([FromServices] IRegisterCentreServer centre) =>
            {
                var res = await centre.UnregisterAll();
                return res.GetResponse();
            }).WithName("清空所有注册").WithOpenApi(operation =>
            {
                operation.Summary = "清空所有注册";
                operation.Description = "清空所有注册";
                operation.Tags = tagGroup;
                return operation;
            });

        });
    }

    /// <summary>
    /// 注册MoRegisterCentreClient
    /// </summary>
    /// <returns></returns>
    public static void AddMoRegisterCentreClient<TServer, TClient>(this IServiceCollection services,
        Action<MoRegisterCentreSetting>? action = null)
        where TServer : class, IRegisterCentreServerConnector where TClient : class, IRegisterCentreClient
    {
        if (MoRegisterCentreManager.HasSetServerOrClient) return;
        var setting = new MoRegisterCentreSetting
        {
            ThisIsCentreClient = true
        };
        action?.Invoke(setting);
        MoRegisterCentreManager.Setting = setting;
        
        services.TryAddSingleton<IRegisterCentreServerConnector, TServer>();
        services.TryAddSingleton<IRegisterCentreClient, TClient>();
    }

    /// <summary>
    /// 配置MoRegisterCentreClient Endpoints中间件
    /// </summary>
    /// <param name="app"></param>
    public static void UseMoRegisterCentreClient(this IApplicationBuilder app)
    {
        if (MoRegisterCentreManager.Setting.ThisIsCentreServer) return;

    }


    /// <summary>
    /// 配置MoRegisterCentreClient Endpoints中间件
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoRegisterCentreClient(this IApplicationBuilder app, string? groupName = "注册中心")
    {
        if (MoRegisterCentreManager.Setting.ThisIsCentreServer) return;
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = groupName, Description = "注册中心客户端相关内置接口" } };
            endpoints.MapGet(MoRegisterCentreConventions.ClientReconnectCentre, async (HttpResponse response, HttpContext context, [FromServices] IRegisterCentreServerConnector connector, [FromServices] IRegisterCentreClient client) =>
            {
                return await connector.Register(client.GetServiceStatus());
            }).WithName("测试重连配置中心").WithOpenApi(operation =>
            {
                operation.Summary = "测试重连配置中心";
                operation.Description = "测试重连配置中心";
                operation.Tags = tagGroup;
                return operation;
            });
        });


        var connector = app.ApplicationServices.GetService<IRegisterCentreServerConnector>();
        if (connector == null) throw new InvalidOperationException($"无法解析{nameof(IRegisterCentreServerConnector)}，可能未注册{nameof(IRegisterCentreServerConnector)}及{nameof(IRegisterCentreClient)}实现");
        Task.Factory.StartNew(async () =>
        {
            await connector.DoingRegister();
        }, TaskCreationOptions.LongRunning);
    }
}