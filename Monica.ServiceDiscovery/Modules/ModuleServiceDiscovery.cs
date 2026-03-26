using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Facades;
using Monica.ServiceDiscovery.Localization;
using Monica.ServiceDiscovery.Models;
using Monica.ServiceDiscovery.Providers;
using Monica.ServiceDiscovery.Services;
using Monica.ServiceDiscovery.Services.Support;
using Monica.StateStore;
using Polly;
using Polly.Retry;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.ServiceDiscovery)]
public class ModuleServiceDiscovery(ModuleServiceDiscoveryOption option) : MoModule<ModuleServiceDiscovery, ModuleServiceDiscoveryOption, ModuleServiceDiscoveryGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<ServiceDiscoveryResource>();

        // Depend on HostedService module for MoBackgroundService base class
        DependsOnModule<ModuleHostedServiceGuide>().Register();

        // Depend on Resilience module for heartbeat retry pipelines
        DependsOnModule<ModuleResilienceGuide>()
            .Register()
            .AddResiliencePipeline(ResiliencePipelineNames.ServiceDiscovery, builder =>
                builder.AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 5,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                }));

        // Depend on StateStore module for state management
        // Only register common state store if not using custom keyed provider
        if (!option.UseCustomKeyedStateStore)
        {
            DependsOnModule<ModuleStateStoreGuide>()
                .Register()
                .AddKeyedCommonStateStore(nameof(ModuleServiceDiscovery), !option.IsStandaloneMode);
        }
        else
        {
            // Just register the StateStore module dependency without adding keyed store
            DependsOnModule<ModuleStateStoreGuide>().Register();
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // If a custom keyed state store is used, proxy it to the ServiceDiscovery service key.
        if (option.UseCustomKeyedStateStore && !string.IsNullOrEmpty(option.CustomStateStoreServiceKey))
        {
            if (option.CustomStateStoreServiceKey != nameof(ModuleServiceDiscovery))
            {
                services.AddKeyedSingleton<IMoStateStore>(nameof(ModuleServiceDiscovery), (sp, _) => sp.GetRequiredKeyedService<IMoStateStore>(option.CustomStateStoreServiceKey));
            }
        }

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

        // Register the default IServiceDiscoveryClientInfo implementation.
        // TryAddSingleton allows callers to provide a custom implementation when needed.
        services.TryAddSingleton<IServiceDiscoveryClientInfo, DefaultServiceDiscoveryClientInfo>();
        services.AddScoped<ServiceDiscoveryQueryService>();
        services.AddScoped<ServiceDiscoveryFacade>();

        // 注册新的 StateStore 基础服务
        services.TryAddSingleton<IRegistrationStateManager, RegistrationStateManager>();
        services.TryAddSingleton<ILeaderElectionService, LeaderElectionService>();

        // 注册 ServiceDiscoveryClientHostedService 为单例并同时作为 HostedService 和 Coordinator
        services.AddSingleton<ServiceDiscoveryClientHostedService>();
        services.AddSingleton<IServiceRegistrationCoordinator>(provider =>
            provider.GetRequiredService<ServiceDiscoveryClientHostedService>());
        services.AddHostedService(provider =>
            provider.GetRequiredService<ServiceDiscoveryClientHostedService>());

        // 注册默认信息提供者实现
        services.TryAddSingleton<IServiceDiscoveryCatalogProvider, DefaultServiceDiscoveryCatalogProvider>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapGet(ServiceDiscoveryConventions.RegistryLeaderStatus,
                async ([FromServices] ServiceDiscoveryFacade facade) =>
                    (await facade.GetRegistryLeaderStatusAsync()).GetResponse())
            .WithName("查询Leader状态")
            .WithTags(tagName)
            .WithSummary("查询 Leader 状态")
            .WithDescription("查询当前实例在服务集群中的 Leader 状态");

            endpoints.MapGet(ServiceDiscoveryConventions.RegistryServiceStatus,
                async ([FromServices] ServiceDiscoveryFacade facade) =>
                    (await facade.GetRegistryServiceStatusAsync()).GetResponse())
            .WithName("获取服务状态")
            .WithTags(tagName)
            .WithSummary("获取服务状态")
            .WithDescription("获取当前实例的注册状态和 Leader 信息");

            endpoints.MapPost("/registry/release-leader",
                async ([FromServices] ServiceDiscoveryFacade facade) =>
                    (await facade.ReleaseLeaderAsync()).GetResponse())
            .WithName("释放Leader状态")
            .WithTags(tagName)
            .WithSummary("释放 Leader 状态")
            .WithDescription("强制当前实例释放 Leader 状态（用于调试/管理）");

            endpoints.MapGet("/registry/election-config",
                ([FromServices] ServiceDiscoveryFacade facade) =>
                    facade.GetElectionConfig().GetResponse())
            .WithName("获取选举配置")
            .WithTags(tagName)
            .WithSummary("获取选举配置")
            .WithDescription("获取当前实例的 Leader 选举配置参数");
        });
    }
}

public class ModuleServiceDiscoveryGuide : MoModuleGuide<ModuleServiceDiscovery, ModuleServiceDiscoveryOption, ModuleServiceDiscoveryGuide>
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
    public ModuleServiceDiscoveryGuide UseInMemoryStateStore()
    {
        ConfigureEmpty(SET_STATE_STORE);
        ConfigureModuleOption(o =>
        {
            o.IsStandaloneMode = true;
            o.IsRegistryServer = true;
        });
        return this;
    }

    /// <summary>
    /// 使用分布式状态存储（多实例模式）
    /// </summary>
    /// <remarks>
    /// 适用于多实例部署，需要配置 Common 分布式 StateStore（如 Redis）
    /// </remarks>
    public ModuleServiceDiscoveryGuide UseDistributedStateStore()
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
    public ModuleServiceDiscoveryGuide SetAsRegistryServer()
    {
        ConfigureModuleOption(o => o.IsRegistryServer = true);
        return this;
    }

    /// <summary>
    /// 设置注册中心服务端目录提供者服务
    /// </summary>
    /// <typeparam name="TInfoProvider">目录提供者服务实现类型</typeparam>
    public ModuleServiceDiscoveryGuide ConfigureRegistryCatalog<TInfoProvider>()
        where TInfoProvider : class, IServiceDiscoveryCatalogProvider
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IServiceDiscoveryCatalogProvider, TInfoProvider>();
        });
        return this;
    }

    /// <summary>
    /// 从Flags枚举设置依赖的子域列表
    /// </summary>
    /// <typeparam name="TEnum">标记了Flags特性的枚举类型</typeparam>
    /// <param name="domainFlags">包含多个域标志的枚举值</param>
    public ModuleServiceDiscoveryGuide SetDependentSubDomains<TEnum>(TEnum domainFlags)
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
    /// 使用已注册的 Keyed StateStore（通过指定 serviceKey）
    /// </summary>
    /// <param name="serviceKey">StateStore 的服务键，用于从 DI 容器中获取对应的 StateStore 实例</param>
    /// <returns>模块指南实例以支持链式调用</returns>
    /// <remarks>
    /// 使用此方法前，需要先在 ModuleStateStoreGuide 中通过 AddKeyedRedisStateStore 或 AddKeyedDaprStateStore 等方法注册对应 serviceKey 的 StateStore。
    /// 默认情况下，可以使用 nameof(ModuleServiceDiscovery) 作为 serviceKey。
    /// </remarks>
    public ModuleServiceDiscoveryGuide UseCustomKeyedStateStore(string serviceKey = nameof(ModuleServiceDiscovery))
    {
        ArgumentNullException.ThrowIfNull(serviceKey);

        ConfigureEmpty(SET_STATE_STORE);
        ConfigureModuleOption(o =>
        {
            o.IsStandaloneMode = false;
            o.CustomStateStoreServiceKey = serviceKey;
        });
        return this;
    }
}
public static class ModuleServiceDiscoveryBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ServiceDiscovery 模块
        /// </summary>
        public static ModuleServiceDiscoveryGuide AddServiceDiscovery(Action<ModuleServiceDiscoveryOption>? action = null)
        {
            return new ModuleServiceDiscoveryGuide().Register(action);
        }
    }
}

public class ModuleServiceDiscoveryOption : MoModuleOptionWithMinimalApi<ModuleServiceDiscovery>
{
    /// <summary>
    /// 当前微服务是注册中心
    /// </summary>
    public bool IsRegistryServer { get; internal set; }

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
    /// Application build time stored in UTC.
    /// Defaults to the entry assembly file last write time in UTC.
    /// Local or unspecified configured values are normalized to UTC.
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
    public bool UseCustomKeyedStateStore => CustomStateStoreServiceKey != null;

    /// <summary>
    /// 自定义 StateStore 的服务键
    /// 用于从 DI 容器中获取指定 serviceKey 的 StateStore 实例
    /// </summary>
    public string? CustomStateStoreServiceKey { get; internal set; }

    #region CoordinatedLeaderService Configuration 
    /// <summary>
    /// Skips waiting for service registration when enabled.
    /// Useful for development and standalone mode. Default: false.
    /// </summary>
    public bool SkipRegistrationWait { get; set; } = false;

    /// <summary>
    /// Maximum time to wait for service registration before starting scheduler services.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan RegistrationWaitTimeout { get; set; } = TimeSpan.FromMinutes(5);
    #endregion
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
