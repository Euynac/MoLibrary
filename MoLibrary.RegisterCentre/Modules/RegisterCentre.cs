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
using MoLibrary.RegisterCentre.Implements.StateStore;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.StateStore.Modules;
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

        // Depend on StateStore module for state management
        // Only register common state store if not using custom keyed provider
        if (!option.UseCustomKeyedStateStore)
        {
            DependsOnModule<ModuleStateStoreGuide>()
                .Register()
                .AddKeyedCommonStateStore(nameof(ModuleRegisterCentre), !option.IsStandaloneMode);
        }
        else
        {
            // Just register the StateStore module dependency without adding keyed store
            DependsOnModule<ModuleStateStoreGuide>().Register();
        }
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

        // 注册新的 StateStore 基础服务
        services.TryAddSingleton<IRegistrationStateManager, RegistrationStateManager>();
        services.TryAddSingleton<ILeaderElectionService, LeaderElectionService>();

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
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "注册中心" } };

            // 获取当前实例的 Leader 状态
            endpoints.MapGet(RegisterCentreConventions.ServerCentreLeaderStatus,
                ([FromServices] ILeaderElectionService leaderService,
                 [FromServices] IRegistrationStateManager stateManager,
                 [FromServices] IRegisterCentreClientInfo clientInfo) =>
                {
                    var response = new LeaderStatusResponse
                    {
                        Status = leaderService.CurrentStatus,
                        LeaderInstanceId = leaderService.IsLeader ? clientInfo.GetServiceStatus().InstanceId : null,
                        LeaderRegistrationTime = leaderService.LeaderBecomeTime,
                        RunningInstanceCount = 1, // 单实例当前只能获取自身信息
                        Message = leaderService.IsLeader ? "当前实例是 Leader" : "当前实例不是 Leader"
                    };
                    return Res.Create(response, ResponseCode.Ok).GetResponse();
                }).WithName("查询Leader状态").WithOpenApi(operation =>
            {
                operation.Summary = "查询 Leader 状态";
                operation.Description = "查询当前实例在服务集群中的 Leader 状态";
                operation.Tags = tagGroup;
                return operation;
            });

            // 获取当前实例的注册状态
            endpoints.MapGet(RegisterCentreConventions.ServerCentreGetServicesStatus, async (
                [FromServices] IRegistrationStateManager stateManager,
                [FromServices] ILeaderElectionService leaderService,
                [FromServices] IRegisterCentreClientInfo clientInfo) =>
            {
                var instances = await stateManager.GetAllInstancesAsync();
                var leaderState = await stateManager.GetLeaderStateAsync();

                var result = new
                {
                    CurrentInstance = new
                    {
                        InstanceInfo = clientInfo.GetServiceStatus(),
                        leaderService.IsLeader,
                        LeaderStatus = leaderService.CurrentStatus.ToString(),
                        leaderService.LeaderBecomeTime
                    },
                    RegisteredInstances = instances,
                    LeaderInfo = leaderState != null ? new
                    {
                        leaderState.InstanceId,
                        leaderState.BecomeLeaderTime,
                        leaderState.ServiceName
                    } : null
                };

                return Res.Create(result, ResponseCode.Ok).GetResponse();
            }).WithName("获取服务状态").WithOpenApi(operation =>
            {
                operation.Summary = "获取服务状态";
                operation.Description = "获取当前实例的注册状态和 Leader 信息";
                operation.Tags = tagGroup;
                return operation;
            });

            // 强制释放 Leader（用于调试/管理）
            endpoints.MapPost("/centre-server/release-leader", async (
                [FromServices] ILeaderElectionService leaderService,
                [FromServices] IRegistrationStateManager stateManager) =>
            {
                if (!leaderService.IsLeader)
                {
                    return Res.Fail("当前实例不是 Leader").GetResponse();
                }

                leaderService.TriggerLeaderLost(Events.LeaderLostReason.GracefulShutdown);
                await stateManager.DeleteLeaderKeyAsync();
                return Res.Ok("已释放 Leader 状态").GetResponse();
            }).WithName("释放Leader状态").WithOpenApi(operation =>
            {
                operation.Summary = "释放 Leader 状态";
                operation.Description = "强制当前实例释放 Leader 状态（用于调试/管理）";
                operation.Tags = tagGroup;
                return operation;
            });

            // 获取选举配置
            endpoints.MapGet("/centre-server/election-config", () =>
            {
                return Res.Create(option.Election, ResponseCode.Ok).GetResponse();
            }).WithName("获取选举配置").WithOpenApi(operation =>
            {
                operation.Summary = "获取选举配置";
                operation.Description = "获取当前实例的 Leader 选举配置参数";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}

public class ModuleRegisterCentreGuide : MoModuleGuide<ModuleRegisterCentre, ModuleRegisterCentreOption, ModuleRegisterCentreGuide>
{
    private const string SET_STATE_STORE = nameof(SET_STATE_STORE);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [SET_STATE_STORE];
    }

    /// <summary>
    /// 使用内存状态存储（单实例模式）
    /// </summary>
    /// <remarks>
    /// 适用于单实例部署或开发环境
    /// </remarks>
    public ModuleRegisterCentreGuide UseInMemoryStateStore()
    {
        ConfigureEmpty(SET_STATE_STORE);
        ConfigureModuleOption(o =>
        {
            o.IsStandaloneMode = true;
        });
        return this;
    }

    /// <summary>
    /// 使用分布式状态存储（多实例模式）
    /// </summary>
    /// <remarks>
    /// 适用于多实例部署，需要配置分布式 StateStore（如 Redis）
    /// </remarks>
    public ModuleRegisterCentreGuide UseDistributedStateStore()
    {
        ConfigureEmpty(SET_STATE_STORE);
        ConfigureModuleOption(o =>
        {
            o.IsStandaloneMode = false;
        });
        return this;
    }

    /// <summary>
    /// 将当前服务设置为注册中心服务器
    /// </summary>
    /// <remarks>
    /// 设置后，服务将作为配置中心或其他服务的注册管理中心
    /// </remarks>
    public ModuleRegisterCentreGuide SetAsCentreServer()
    {
        ConfigureModuleOption(o => o.IsCentreServer = true);
        return this;
    }

    /// <summary>
    /// 设置注册中心服务端目录提供者服务
    /// </summary>
    /// <typeparam name="TInfoProvider">目录提供者服务实现类型</typeparam>
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

    /// <summary>
    /// 使用自定义的 Keyed StateStore 提供者
    /// </summary>
    /// <param name="configureKeyedServices">配置 Keyed 服务的委托</param>
    /// <remarks>
    /// 此方法允许外部模块（如 MoLibrary.Dapr）为 RegisterCentre 注册自定义的 StateStore 实现。
    /// 调用此方法后，ClaimDependencies 将不再自动注册默认的 Keyed StateStore。
    /// </remarks>
    public ModuleRegisterCentreGuide UseKeyedStateStore(Action<IServiceCollection> configureKeyedServices)
    {
        ConfigureEmpty(SET_STATE_STORE);
        ConfigureModuleOption(o =>
        {
            o.UseCustomKeyedStateStore = true;
            o.IsStandaloneMode = false;
        });
        ConfigureServices(context =>
        {
            configureKeyedServices(context.Services);
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
    /// 需要读取作为元数据的环境变量Key列表
    /// </summary>
    public List<string> MetadataEnvironmentVariables { get; set; } = new();

    /// <summary>
    /// 是否获取监听地址作为元数据
    /// </summary>
    public bool IncludeListeningAddresses { get; set; } = true;
    

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

    // === 新架构配置 ===

    /// <summary>
    /// Leader 选举配置
    /// </summary>
    public ElectionConfig Election { get; set; } = new();

    /// <summary>
    /// 孤立处理模式
    /// </summary>
    public EIsolationHandlingMode IsolationHandlingMode { get; set; } = EIsolationHandlingMode.ContinueRunning;

    /// <summary>
    /// 是否使用自定义的 Keyed StateStore 提供者
    /// 当为 true 时，ClaimDependencies 不会自动注册 Keyed StateStore
    /// </summary>
    public bool UseCustomKeyedStateStore { get; internal set; }
}

/// <summary>
/// 孤立处理模式
/// </summary>
public enum EIsolationHandlingMode
{
    /// <summary>
    /// 继续运行（降级模式，不参与 Leader 选举）
    /// </summary>
    ContinueRunning,

    /// <summary>
    /// 快速下线
    /// </summary>
    FastShutdown
}