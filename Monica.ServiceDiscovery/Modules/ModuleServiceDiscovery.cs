using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Results;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Facades;
using Monica.ServiceDiscovery.Localization;
using Monica.ServiceDiscovery.Models;
using Monica.ServiceDiscovery.Providers;
using Monica.ServiceDiscovery.Services;
using Monica.ServiceDiscovery.Services.Support;
using Monica.StateStore.Abstractions;
using Polly;
using Polly.Retry;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public class ModuleServiceDiscovery : MonicaModule<ModuleServiceDiscoveryOption>, IWebModule
{
    internal const string STATE_STORE_FEATURE = "service-discovery-state-store";

    public override void Describe(ModuleDescriptor module)
    {
        module.RequireFeature(STATE_STORE_FEATURE);
        module.Require<ModuleLocalization, ModuleLocalizationOption>(localization =>
        {
            if (!localization.ResourceMarkerTypes.Contains(typeof(ServiceDiscoveryResource)))
            {
                localization.ResourceMarkerTypes.Add(typeof(ServiceDiscoveryResource));
            }
        });
        module.Require<ModuleHostedService, ModuleHostedServiceOption>();
        module.Require<ModuleResilience, ModuleResilienceOption>(resilience =>
            resilience.PipelineConfigurations[ResiliencePipelineNames.ServiceDiscovery] = builder =>
                builder.AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 5,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                }));
        module.Require<ModuleStateStore, ModuleStateStoreOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleServiceDiscoveryOption> context)
    {
        var services = context.Services;
        // If a custom keyed state store is used, proxy it to the ServiceDiscovery service key.
        if (Option.UseCustomKeyedStateStore && !string.IsNullOrEmpty(Option.CustomStateStoreServiceKey))
        {
            if (Option.CustomStateStoreServiceKey != nameof(ModuleServiceDiscovery))
            {
                services.AddKeyedSingleton<IStateStore>(nameof(ModuleServiceDiscovery), (provider, _) =>
                    provider.GetRequiredKeyedService<IStateStore>(Option.CustomStateStoreServiceKey));
            }
        }
        else
        {
            services.AddKeyedSingleton<IStateStore>(nameof(ModuleServiceDiscovery), (provider, _) =>
                provider.GetRequiredService<IStateStore>());
        }

        if (Option.IncludeListeningAddresses)
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

    public override void ConfigureEndpoints(WebModuleContext<ModuleServiceDiscoveryOption> context)
    {
        UseEndpoints(context, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

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

public static class ModuleServiceDiscoveryBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the ServiceDiscovery module.
        /// </summary>
        public ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> AddServiceDiscovery(
            Action<ModuleServiceDiscoveryOption>? action = null)
        {
            return builder.AddModule<ModuleServiceDiscovery, ModuleServiceDiscoveryOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> registration)
    {
        /// <summary>
        /// Uses the process-local state store and makes this instance the registry server.
        /// </summary>
        public ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> UseInMemoryStateStore()
        {
            return registration
                .Configure(options =>
                {
                    options.IsStandaloneMode = true;
                    options.IsRegistryServer = true;
                    options.CustomStateStoreServiceKey = null;
                })
                .SatisfyFeature(ModuleServiceDiscovery.STATE_STORE_FEATURE);
        }

        /// <summary>
        /// Uses the StateStore module's configured distributed provider.
        /// </summary>
        public ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> UseDistributedStateStore()
        {
            registration.Require<ModuleStateStore, ModuleStateStoreOption>()
                .RequireFeature(ModuleStateStore.DISTRIBUTED_PROVIDER_FEATURE);
            return registration
                .Configure(options =>
                {
                    options.IsStandaloneMode = false;
                    options.CustomStateStoreServiceKey = null;
                })
                .SatisfyFeature(ModuleServiceDiscovery.STATE_STORE_FEATURE);
        }

        public ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> SetAsRegistryServer()
        {
            return registration.Configure(options => options.IsRegistryServer = true);
        }

        public ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> ConfigureRegistryCatalog<TProvider>()
            where TProvider : class, IServiceDiscoveryCatalogProvider
        {
            return registration.ConfigureServices(context =>
                context.Services.AddSingleton<IServiceDiscoveryCatalogProvider, TProvider>());
        }

        public ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> SetDependentSubDomains<TEnum>(
            TEnum domainFlags)
            where TEnum : struct, Enum
        {
            if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
            {
                throw new ArgumentException("The enum type must be marked with FlagsAttribute.", nameof(domainFlags));
            }

            var domains = Enum.GetValues<TEnum>()
                .Where(flag => Convert.ToInt32(flag) != 0 && domainFlags.HasFlag(flag))
                .Select(static flag => flag.ToString())
                .ToList();
            return registration.Configure(options => options.DependentSubDomains = domains);
        }

        /// <summary>
        /// Uses a keyed state store that the host has registered explicitly.
        /// </summary>
        public ModuleRegistration<ModuleServiceDiscovery, ModuleServiceDiscoveryOption> UseCustomKeyedStateStore(
            string serviceKey = nameof(ModuleServiceDiscovery))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
            return registration
                .Configure(options =>
                {
                    options.IsStandaloneMode = false;
                    options.CustomStateStoreServiceKey = serviceKey;
                })
                .SatisfyFeature(ModuleServiceDiscovery.STATE_STORE_FEATURE);
        }
    }
}

public class ModuleServiceDiscoveryOption : MinimalApiModuleOptions<ModuleServiceDiscovery>
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
    /// When not configured, ServiceDiscovery uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Unique application identifier.
    /// When not configured, ServiceDiscovery uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// Display name of the application.
    /// When not configured, ServiceDiscovery uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// Project name.
    /// When not configured, ServiceDiscovery uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
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
    /// When not configured, ServiceDiscovery uses the application version configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
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
    /// When true, the host supplies an explicitly keyed StateStore instead of using the module default.
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
