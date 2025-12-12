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
using MoLibrary.Core.Modules;
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

    public override void ClaimDependencies()
    {
        // Depend on HostedService module for MoBackgroundService base class
        DependsOnModule<ModuleHostedServiceGuide>().Register();
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

        // 注册默认的IRegisterCentreClientInfo实现
        // 使用TryAddSingleton允许用户在需要时提供自定义实现
        services.TryAddSingleton<IRegisterCentreClientInfo, DefaultRegisterCentreClientInfo>();

        // 注册 RegisterCentreClientHostedService 为单例并同时作为 HostedService 和 Coordinator
        services.AddSingleton<RegisterCentreClientHostedService>();
        services.AddSingleton<IServiceRegistrationCoordinator>(provider =>
            provider.GetRequiredService<RegisterCentreClientHostedService>());
        services.AddHostedService(provider =>
            provider.GetRequiredService<RegisterCentreClientHostedService>());

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
        return [nameof(SET_PROVIDER)];
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

    /// <summary>
    /// 从Flags枚举设置依赖的子域列表
    /// </summary>
    /// <typeparam name="TEnum">标记了Flags特性的枚举类型</typeparam>
    /// <param name="domainFlags">包含多个域标志的枚举值</param>
    /// <returns></returns>
    public ModuleRegisterCentreGuide SetDependentSubDomains<TEnum>(TEnum domainFlags)
        where TEnum : struct, Enum
    {
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException("枚举类型必须标记为Flags", nameof(domainFlags));
        }

        var domains = new List<string>();
        var flagValues = Enum.GetValues<TEnum>();

        foreach (var flagValue in flagValues)
        {
            // 跳过None值（通常为0）
            if (Convert.ToInt32(flagValue) == 0) continue;

            // 检查是否包含该标志
            if (domainFlags.HasFlag(flagValue))
            {
                domains.Add(flagValue.ToString());
            }
        }

        ConfigureModuleOption(o => o.DependentSubDomains = domains);
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
    /// 客户端心跳间隔（单位：秒）
    /// </summary>
    public int HeartbeatInterval { get; set; } = 10;

    /// <summary>
    /// 客户端注册中心重试次数
    /// </summary>
    public int ClientRetryTimes { get; set; } = 0;
    /// <summary>
    /// 客户端注册重试间隔（单位：秒）
    /// </summary>
    public int InitialRetryInterval { get; set; } = 5;
    
    /// <summary>
    /// 服务端心跳检查间隔（单位：秒）
    /// </summary>
    public int ServerHeartbeatCheckInterval { get; set; } = 5;

    /// <summary>
    /// 不健康阈值倍数 - 心跳超过 (HeartbeatInterval × UnhealthyThresholdMultiplier) 秒后实例被标记为Unhealthy
    /// 默认值：1.5（即 10秒心跳间隔 × 1.5 = 15秒后标记为不健康）
    /// </summary>
    public double UnhealthyThresholdMultiplier { get; set; } = 1.5;

    /// <summary>
    /// 离线阈值倍数 - 心跳超过 (HeartbeatInterval × OfflineThresholdMultiplier) 秒后实例被标记为Offline
    /// 默认值：2.5（即 10秒心跳间隔 × 2.5 = 25秒后标记为离线）
    /// </summary>
    public double OfflineThresholdMultiplier { get; set; } = 2.5;

    /// <summary>
    /// 驱逐阈值倍数 - 心跳超过 (HeartbeatInterval × ExpelThresholdMultiplier) 秒后实例被从注册中心移除
    /// 默认值：5.0（即 10秒心跳间隔 × 5.0 = 50秒后从注册中心移除）
    /// </summary>
    public double ExpelThresholdMultiplier { get; set; } = 5.0;

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

    // === Service Identity Configuration ===

    /// <summary>
    /// 子域名（可选）
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// 微服务唯一标识符（默认从入口程序集名称获取）
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// 微服务显示名称（默认从入口程序集名称获取）
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// 项目名称（默认从入口程序集名称获取）
    /// </summary>
    public string? ProjectName { get; set; }

    // === Version Information ===

    /// <summary>
    /// 应用构建时间（默认从程序集文件修改时间获取）
    /// </summary>
    public DateTime? BuildTime { get; set; }

    /// <summary>
    /// 程序集版本号（默认从FileVersionInfo获取）
    /// </summary>
    public string? AssemblyVersion { get; set; }

    /// <summary>
    /// 发布版本号（自定义版本标识）
    /// </summary>
    public string? ReleaseVersion { get; set; }

    // === Instance Information ===

    /// <summary>
    /// 实例标识符，格式："hostname:processId"
    /// 如未设置，自动生成为："{COMPUTERNAME/HOSTNAME/MachineName}:{ProcessId}"
    /// </summary>
    public string? FromInstance { get; set; }

    /// <summary>
    /// 依赖的子域列表
    /// </summary>
    public List<string>? DependentSubDomains { get; set; }

    // === Distributed Mode Configuration ===

    /// <summary>
    /// 注册中心服务的AppId（分布式模式下连接的注册中心标识）
    /// 使用UseDistributedProvider时必须配置
    /// </summary>
    public string? RegisterCentreAppId { get; set; }
}