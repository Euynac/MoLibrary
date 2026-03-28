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
using Monica.Core.Results;
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
            // Register IServerAddressesFeature to retrieve listening addresses.
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

        // Register the new StateStore foundational services.
        services.TryAddSingleton<IRegistrationStateManager, RegistrationStateManager>();
        services.TryAddSingleton<ILeaderElectionService, LeaderElectionService>();

        // Register ServiceDiscoveryClientHostedService as a singleton and expose it as both HostedService and Coordinator.
        services.AddSingleton<ServiceDiscoveryClientHostedService>();
        services.AddSingleton<IServiceRegistrationCoordinator>(provider =>
            provider.GetRequiredService<ServiceDiscoveryClientHostedService>());
        services.AddHostedService(provider =>
            provider.GetRequiredService<ServiceDiscoveryClientHostedService>());

        // Register the default information provider implementation.
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
    /// Uses in-memory state storage (single-instance mode).
    /// </summary>
    /// <remarks>
    /// Suitable for single-instance deployments or development environments.
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
    /// Uses distributed state storage (multi-instance mode).
    /// </summary>
    /// <remarks>
    /// Suitable for multi-instance deployments and requires a common distributed StateStore (for example, Redis).
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
    /// Marks the current service as the registry server.
    /// </summary>
    public ModuleServiceDiscoveryGuide SetAsRegistryServer()
    {
        ConfigureModuleOption(o => o.IsRegistryServer = true);
        return this;
    }

    /// <summary>
    /// Configures the catalog provider service used by the registry server.
    /// </summary>
    /// <typeparam name="TInfoProvider">Implementation type of the catalog provider service.</typeparam>
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
    /// Sets the dependent subdomain list from a Flags enum value.
    /// </summary>
    /// <typeparam name="TEnum">Enum type decorated with the Flags attribute.</typeparam>
    /// <param name="domainFlags">Enum value that may include multiple domain flags.</param>
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
            // Skip the None value (typically 0).
            if (Convert.ToInt32(flagValue) == 0) continue;

            // Check whether this flag is included.
            if (domainFlags.HasFlag(flagValue))
            {
                domains.Add(flagValue.ToString());
            }
        }

        ConfigureModuleOption(o => o.DependentSubDomains = domains);
        return this;
    }
    
    /// <summary>
    /// Uses a pre-registered keyed StateStore via the specified serviceKey.
    /// </summary>
    /// <param name="serviceKey">Service key of the StateStore used to resolve the corresponding instance from the DI container.</param>
    /// <returns>The module guide instance for fluent chaining.</returns>
    /// <remarks>
    /// Before calling this method, register the StateStore for the target serviceKey in ModuleStateStoreGuide,
    /// for example via AddKeyedRedisStateStore or AddKeyedDaprStateStore.
    /// By default, nameof(ModuleServiceDiscovery) can be used as the serviceKey.
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
        /// Configures the ServiceDiscovery module.
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
    /// Indicates whether the current microservice acts as the registry server.
    /// </summary>
    public bool IsRegistryServer { get; internal set; }

    /// <summary>
    /// Indicates whether standalone in-memory mode is enabled.
    /// Suitable for single-instance deployments or development environments and does not support cross-service configuration calls.
    /// </summary>
    public bool IsStandaloneMode { get; internal set; }
   
    /// <summary>
    /// Environment variable keys to read as metadata.
    /// </summary>
    public List<string> MetadataEnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Indicates whether listening addresses should be included as metadata.
    /// </summary>
    public bool IncludeListeningAddresses { get; set; } = true;
    

    // === Service Identity Configuration ===

    /// <summary>
    /// Subdomain name (optional).
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Unique microservice identifier (defaults to the entry assembly name).
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// Display name of the microservice (defaults to the entry assembly name).
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// Project name (defaults to the entry assembly name).
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
    /// Assembly version (defaults to FileVersionInfo).
    /// </summary>
    public string? AssemblyVersion { get; set; }

    /// <summary>
    /// Release version (custom version identifier).
    /// </summary>
    public string? ReleaseVersion { get; set; }

    // === Instance Information ===

    /// <summary>
    /// Instance identifier in the format "hostname:processId".
    /// If not set, it is generated automatically as "{COMPUTERNAME/HOSTNAME/MachineName}:{ProcessId}".
    /// </summary>
    public string? FromInstance { get; set; }

    /// <summary>
    /// List of dependent subdomains.
    /// </summary>
    public List<string>? DependentSubDomains { get; set; }

    // === New Architecture Configuration ===

    /// <summary>
    /// Leader election configuration.
    /// </summary>
    public ElectionConfig Election { get; set; } = new();

    /// <summary>
    /// Isolation handling mode.
    /// </summary>
    public EIsolationHandlingMode IsolationHandlingMode { get; set; } = EIsolationHandlingMode.ContinueRunning;

    /// <summary>
    /// Indicates whether a custom keyed StateStore provider is used.
    /// When true, ClaimDependencies does not auto-register a keyed StateStore.
    /// </summary>
    public bool UseCustomKeyedStateStore => CustomStateStoreServiceKey != null;

    /// <summary>
    /// Service key of the custom StateStore.
    /// Used to resolve the keyed StateStore instance from the DI container.
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
/// Isolation handling mode.
/// </summary>
public enum EIsolationHandlingMode
{
    /// <summary>
    /// Continue running in degraded mode without participating in leader election.
    /// </summary>
    ContinueRunning,

    /// <summary>
    /// Shut down quickly.
    /// </summary>
    FastShutdown
}
