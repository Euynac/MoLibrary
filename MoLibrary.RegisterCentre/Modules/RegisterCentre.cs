using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.RegisterCentre.Implements;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Modules;

public class ModuleRegisterCentre(ModuleRegisterCentreOption option) : MoModule<ModuleRegisterCentre, ModuleRegisterCentreOption, ModuleRegisterCentreGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.RegisterCentre;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        if (option is { IncludeListeningAddresses: true})
        {
            // 注册 IServerAddressesFeature 以获取监听地址
            services.TryAddSingleton(provider =>
            {
                var server = provider.GetService<IServer>();
                return server?.Features.Get<IServerAddressesFeature>() 
                       ?? new ServerAddressesFeature();
            });
        }
        
        services.AddHostedService<RegisterCentreClientHostedService>();

        // 注册默认信息提供者实现
        services.TryAddSingleton<IRegisterCentreCatalogProvider, DefaultRegisterCentreCatalogProvider>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (option.IsCentreServer)
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "注册中心" } };
                endpoints.MapPost(RegisterCentreConventions.ServerCentreRegister, async (ServiceRegisterInfo req, [FromServices] IRegisterCentreServer centre) =>
                {
                    if ((await centre.Register(req)).IsFailed(out var error)) return error;
                    return Res.Ok("注册成功");
                }).WithName("微服务注册").WithOpenApi(operation =>
                {
                    operation.Summary = "微服务注册";
                    operation.Description = "注册微服务到注册中心";
                    operation.Tags = tagGroup;
                    return operation;
                });
                
                endpoints.MapPost(RegisterCentreConventions.ServerCentreHeartbeat, async (ServiceHeartbeat req, [FromServices] IRegisterCentreServer centre) =>
                {
                    if ((await centre.Heartbeat(req)).IsFailed(out var error, out var data))
                        return error.GetResponse();
                    return Res.Create(data, ResponseCode.Ok).GetResponse();
                }).WithName("微服务心跳").WithOpenApi(operation =>
                {
                    operation.Summary = "微服务心跳";
                    operation.Description = "发送心跳到注册中心";
                    operation.Tags = tagGroup;
                    return operation;
                });

                endpoints.MapPost(RegisterCentreConventions.ServerCentreLeaderStatus, async (LeaderStatusRequest req, [FromServices] IRegisterCentreServer centre) =>
                {
                    if ((await centre.GetLeaderStatus(req)).IsFailed(out var error, out var data))
                        return error.GetResponse();
                    return Res.Create(data, ResponseCode.Ok).GetResponse();
                }).WithName("查询领导者状态").WithOpenApi(operation =>
                {
                    operation.Summary = "查询领导者状态";
                    operation.Description = "查询指定实例在服务集群中的领导者状态（Leader/Follower/Looking）";
                    operation.Tags = tagGroup;
                    return operation;
                });

                endpoints.MapGet(RegisterCentreConventions.ServerCentreGetServicesStatus, async ([FromServices] IRegisterCentreServer centre) =>
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


                endpoints.MapGet(RegisterCentreConventions.ServerCentreUnregisterAll, async ([FromServices] IRegisterCentreServer centre) =>
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
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "注册中心客户端相关内置接口" } };
            endpoints.MapGet(RegisterCentreConventions.ClientReconnectCentre, async (HttpResponse response, HttpContext context, [FromServices] IRegisterCentreServerConnector connector, [FromServices] IRegisterCentreClientInfo client) =>
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

public class ModuleRegisterCentreGuide : MoModuleGuide<ModuleRegisterCentre, ModuleRegisterCentreOption, ModuleRegisterCentreGuide>
{
    private const string SET_PROVIDER =  nameof(SET_PROVIDER);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(SET_PROVIDER), nameof(ConfigClientInfo)];
    }
    /// <summary>
    /// 设置注册中心客户端信息
    /// </summary>
    /// <typeparam name="TClientInfo">注册中心客户端信息实现类型</typeparam>
    /// <returns></returns>
    public ModuleRegisterCentreGuide ConfigClientInfo<TClientInfo>() where TClientInfo : class, IRegisterCentreClientInfo
    {
        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IRegisterCentreClientInfo, TClientInfo>();
        });
        return this;
    }

    /// <summary>
    /// 使用单实例内存模式
    /// </summary>
    /// <returns></returns>
    public ModuleRegisterCentreGuide UseInMemoryProvider()
    {
        ConfigureEmpty(SET_PROVIDER);
        ConfigureModuleOption(o =>
        {
            o.IsStandaloneMode = true;
        });
        SetAsCentreServer();
        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<ILeaderService, ClientSideLeaderService>();
            context.Services.TryAddSingleton<IRegisterCentreServerConnector, RegisterCentreServerConnectorStandaloneProvider>();
            context.Services.TryAddSingleton<IRegisterCentreServerInvocationConnector, RegisterCentreServerInvocationConnectorStandaloneProvider>();
        });
        return this;
    }

    /// <summary>
    /// 使用分布式模式(多实例场景)
    /// </summary>
    /// <typeparam name="TProvider"></typeparam>
    /// <returns></returns>
    public ModuleRegisterCentreGuide UseDistributedProvider<TProvider>()
        where TProvider : class, IRegisterCentreServerInvocationConnector
    {
        ConfigureEmpty(SET_PROVIDER);
        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<ILeaderService, ClientSideLeaderService>();
            context.Services.TryAddSingleton<IRegisterCentreServerInvocationConnector, TProvider>();
            context.Services.TryAddSingleton<IRegisterCentreServerConnector, RegisterCentreServerConnectorDistributedProvider>();
        });
        return this;
    }
    /// <summary>
    /// 设置当前服务为注册中心服务端
    /// </summary>
    /// <returns></returns>
    public ModuleRegisterCentreGuide SetAsCentreServer()
    {
        ConfigureModuleOption(o =>
        {
            o.IsCentreServer = true;
        });

        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IRegisterCentreServer, MemoryProviderForRegisterCentre>();
        });
        return this;
    }
   
    /// <summary>
    /// 设置注册中心服务端目录提供者服务
    /// </summary>
    /// <typeparam name="TInfoProvider">目录提供者服务实现类型</typeparam>
    /// <returns></returns>
    public ModuleRegisterCentreGuide ConfigServerCatalog<TInfoProvider>()
        where TInfoProvider : class, IRegisterCentreCatalogProvider
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IRegisterCentreCatalogProvider, TInfoProvider>();
        });
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
    /// 当前微服务是注册中心
    /// </summary>
    public bool IsCentreServer { get; internal set; }

    /// <summary>
    /// 是否是单实例内存模式(standalone mode)
    /// 适用于单实例部署或开发环境，不支持跨服务配置调用
    /// </summary>
    public bool IsStandaloneMode { get; internal set; }
   
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
    
    /// <summary>
    /// 服务端心跳检查间隔（单位：ms）
    /// </summary>
    public int ServerHeartbeatCheckInterval { get; set; } = 5000;

    /// <summary>
    /// 不健康阈值（单位：ms）- 心跳超过此时间后实例被标记为Unhealthy
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 6000;

    /// <summary>
    /// 离线阈值（单位：ms）- 心跳超过此时间后实例被标记为Offline
    /// </summary>
    public int OfflineThreshold { get; set; } = 16000;

    /// <summary>
    /// 驱逐阈值（单位：ms）- 心跳超过此时间后实例被从注册中心移除
    /// </summary>
    public int ExpelThreshold { get; set; } = 45000;

    /// <summary>
    /// 需要读取作为元数据的环境变量Key列表
    /// </summary>
    public List<string> MetadataEnvironmentVariables { get; set; } = new();

    /// <summary>
    /// 是否获取监听地址作为元数据
    /// </summary>
    public bool IncludeListeningAddresses { get; set; } = true;

    /// <summary>
    /// 是否启用领导者选举功能
    /// <para>启用后，注册中心会自动为每个AppId的实例进行领导者选举</para>
    /// <para>选举规则：注册时间最早的Running状态实例被选为领导者</para>
    /// </summary>
    public bool EnableLeaderElection { get; set; } = true;
}