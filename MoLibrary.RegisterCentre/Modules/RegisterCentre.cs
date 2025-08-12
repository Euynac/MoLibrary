using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.Modules;
using MoLibrary.RegisterCentre.Implements;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Modules;

public class ModuleRegisterCentre(ModuleRegisterCentreOption option) : MoModuleWithDependencies<ModuleRegisterCentre, ModuleRegisterCentreOption, ModuleRegisterCentreGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.RegisterCentre;
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        if (option.ThisIsCentreClient)
        {
            var connector = app.ApplicationServices.GetService<IRegisterCentreServerConnector>();
            if (connector == null) throw new InvalidOperationException($"无法解析{nameof(IRegisterCentreServerConnector)}，可能未注册{nameof(IRegisterCentreServerConnector)}及{nameof(IRegisterCentreClient)}实现");
            Task.Factory.StartNew(async () =>
            {
                await connector.DoingRegister();
            }, TaskCreationOptions.LongRunning);
        }
        
    }
    
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (option.ThisIsCentreServer)
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "注册中心" } };
                endpoints.MapPost(MoRegisterCentreConventions.ServerCentreRegister, async (ServiceRegisterInfo req, [FromServices] IRegisterCentreServer centre) =>
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
        else if (option.ThisIsCentreClient)
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "注册中心客户端相关内置接口" } };
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
        }
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
    }
}

public class ModuleRegisterCentreGuide : MoModuleGuide<ModuleRegisterCentre, ModuleRegisterCentreOption, ModuleRegisterCentreGuide>
{
    public const string SET_CENTRE_TYPE = nameof(SET_CENTRE_TYPE);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [SET_CENTRE_TYPE];
    }

    /// <summary>
    /// 注册MoRegisterCentreClient
    /// </summary>
    /// <returns></returns>
    public ModuleRegisterCentreGuide SetAsCentreServer()
    {
        ConfigureModuleOption(o =>
        {
            o.ThisIsCentreServer = true;
        }, key: SET_CENTRE_TYPE);

        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IRegisterCentreServer, MemoryProviderForRegisterCentre>();
            context.Services.AddSingleton<IRegisterCentreClientConnector, DaprHttpForConnectClient>();
        }, key: SET_CENTRE_TYPE);

        return this;
    }
    /// <summary>
    /// 注册MoRegisterCentreClient
    /// </summary>
    /// <returns></returns>
    public ModuleRegisterCentreGuide SetAsCentreClient<TServer, TClient>()
        where TServer : class, IRegisterCentreServerConnector where TClient : class, IRegisterCentreClient
    {
        ConfigureModuleOption(o =>
        {
            o.ThisIsCentreClient = true;
        }, key: SET_CENTRE_TYPE);
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IRegisterCentreServerConnector, TServer>();
            context.Services.AddSingleton<IRegisterCentreClient, TClient>();
        }, key: SET_CENTRE_TYPE);
        return this;
    }
}
public static class ModuleRegisterCentreBuilderExtensions
{
    public static ModuleRegisterCentreGuide ConfigModuleRegisterCentre(this WebApplicationBuilder builder, Action<ModuleRegisterCentreOption>? action = null)
    {
        return new ModuleRegisterCentreGuide().Register(action);
    }
}

public class ModuleRegisterCentreOption : MoModuleControllerOption<ModuleRegisterCentre>
{
    /// <summary>
    /// 设定当前微服务是注册中心
    /// </summary>
    internal bool ThisIsCentreServer { get; set; } = false;
    /// <summary>
    /// 设定当前微服务是注册中心客户端
    /// </summary>
    internal bool ThisIsCentreClient { get; set; } = false;

    /// <summary>
    /// TODO 最大并发执行数量
    /// </summary>
    public int MaxParallelInvokerCount { get; set; }

    /// <summary>
    /// 客户端心跳频率（单位：ms）
    /// </summary>
    public int HeartbeatDuration { get; set; } = 10000;

    /// <summary>
    /// 客户端注册中心重试次数
    /// </summary>
    public int ClientRetryTimes { get; set; } = 3;
    /// <summary>
    /// 客户端重试频率（单位：ms）
    /// </summary>
    public int RetryDuration { get; set; } = 5000;
}