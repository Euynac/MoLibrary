using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.StateStore;
using Monica.StateStore.StackExchange;
using Monica.StateStore.StackExchange.Connection;
using Monica.StateStore.Abstractions;
using StackExchange.Redis;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleRedisStateStoreBuilderExtensions
{
    /// <summary>
    /// Use Redis state store as the global distributed state store provider
    /// </summary>
    /// <param name="guide">StateStore module guide</param>
    /// <param name="action">Redis state store configuration delegate</param>
    /// <returns>Redis StateStore module guide instance for chaining</returns>
    public static ModuleRedisStateStoreGuide UseRedisStateStoreProvider(this ModuleStateStoreGuide guide,
        Action<ModuleRedisStateStoreOption>? action = null)
    {
        guide.SetCommonDistributedStateStoreProvider<RedisStateStore>();

        // Register global IConnectionMultiplexer for common provider
        guide.ConfigureStateStoreServices(services =>
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var factory = sp.GetRequiredService<IRedisConnectionFactory>();
                var options = sp.GetRequiredService<IOptions<ModuleRedisStateStoreOption>>().Value;
                return factory.CreateConnection(options);
            });
        });

        return new ModuleRedisStateStoreGuide().Register(action);
    }

    /// <summary>
    /// Add Redis state store as a keyed StateStore provider
    /// </summary>
    /// <param name="guide">StateStore module guide</param>
    /// <param name="serviceKey">Service key to identify this StateStore instance</param>
    /// <param name="configureOptions">Redis state store configuration delegate</param>
    /// <returns>StateStore module guide instance for chaining</returns>
    public static ModuleStateStoreGuide AddKeyedRedisStateStore(
        this ModuleStateStoreGuide guide,
        string serviceKey,
        Action<ModuleRedisStateStoreOption> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);

        new ModuleRedisStateStoreGuide().Register();
        guide.ConfigureStateStoreServices(services =>
        {
            // Register keyed options
            services.Configure(serviceKey, configureOptions);

            // Register keyed IConnectionMultiplexer using factory
            services.AddKeyedSingleton<IConnectionMultiplexer>(serviceKey, (sp, _) =>
            {
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ModuleRedisStateStoreOption>>();
                var keyedOptions = optionsMonitor.Get(serviceKey);
                var factory = sp.GetRequiredService<IRedisConnectionFactory>();
                return factory.CreateConnection(keyedOptions);
            });

            // Register keyed RedisStateStore
            services.AddKeyedSingleton<IStateStore>(serviceKey, (sp, _) =>
            {
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ModuleRedisStateStoreOption>>();
                var keyedOptions = Options.Create(optionsMonitor.Get(serviceKey));
                var keyedConnection = sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
                return ActivatorUtilities.CreateInstance<RedisStateStore>(sp, keyedConnection, keyedOptions);
            });
        }, serviceKey);

        guide.RecordKeyedServiceKey(serviceKey);
        return guide;
    }
}

[ModuleKey(EMoModuleKey.RedisStateStore)]
public class ModuleRedisStateStore(ModuleRedisStateStoreOption option)
    : MoModule<ModuleRedisStateStore, ModuleRedisStateStoreOption, ModuleRedisStateStoreGuide>(option),
      IStateStoreModuleProvider
{

    public override void ClaimDependencies()
    {
        // Depends on StateStore basic module
        DependsOnModule<ModuleStateStoreGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Only register connection factory (infrastructure)
        // IConnectionMultiplexer and IDistributedStateStore are registered in UseRedisStateStoreProvider
        services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
    }

    #region IStateStoreModuleProvider Implementation

    public ModuleKey ProvidesFor => EMoModuleKey.StateStore;

    public EStateStoreProviderType ProviderType => EStateStoreProviderType.Redis;

    public EStateStoreCapabilities Capabilities =>
        EStateStoreCapabilities.KeyScanning |
        EStateStoreCapabilities.RawStringRetrieval |
        EStateStoreCapabilities.BulkOperations;

    public string DisplayName => "Redis";

    #endregion
}

public class ModuleRedisStateStoreGuide : MoModuleGuide<ModuleRedisStateStore, ModuleRedisStateStoreOption, ModuleRedisStateStoreGuide>
{
}

/// <summary>
/// Redis state store module configuration options
/// </summary>
public class ModuleRedisStateStoreOption : MoModuleOption<ModuleRedisStateStore>
{
    /// <summary>
    /// Redis connection type (Normal, Sentinel, Cluster). Default: Normal
    /// </summary>
    public ERedisConnectionType ConnectionType { get; set; } = ERedisConnectionType.Normal;

    /// <summary>
    /// Detailed connection configuration (for all connection modes).
    /// </summary>
    public RedisConnectionConfiguration? Connection { get; set; }

    /// <summary>
    /// Key prefix for all state store keys (optional)
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Default TTL for state entries (optional, null = no expiration)
    /// </summary>
    public TimeSpan? DefaultTTL { get; set; }

    /// <summary>
    /// Redis database index to use (default: 0)
    /// </summary>
    public int DatabaseIndex { get; set; } = 0;

    /// <summary>
    /// Configure for simple single-node Redis connection
    /// </summary>
    /// <param name="host">Redis host (default: localhost)</param>
    /// <param name="port">Redis port (default: 6379)</param>
    /// <param name="password">Redis password (optional)</param>
    public ModuleRedisStateStoreOption UseNormalConnection(
        string host = "localhost",
        int port = 6379,
        string? password = null)
    {
        ConnectionType = ERedisConnectionType.Normal;
        Connection = new RedisConnectionConfiguration
        {
            Host = host,
            Port = port,
            Password = password
        };
        return this;
    }

    /// <summary>
    /// Configure for Redis Sentinel high availability
    /// </summary>
    /// <param name="sentinelHost">Primary Sentinel host</param>
    /// <param name="sentinelPort">Sentinel port (default: 26379)</param>
    /// <param name="serviceName">Master service name (default: mymaster)</param>
    /// <param name="password">Redis password (optional)</param>
    /// <param name="additionalSentinels">Additional sentinel endpoints (format: host:port)</param>
    public ModuleRedisStateStoreOption UseSentinelConnection(
        string sentinelHost,
        int sentinelPort = 26379,
        string serviceName = "mymaster",
        string? password = null,
        params string[] additionalSentinels)
    {
        ConnectionType = ERedisConnectionType.Sentinel;
        Connection = new RedisConnectionConfiguration
        {
            Host = sentinelHost,
            Port = sentinelPort,
            ServiceName = serviceName,
            Password = password,
            AdditionalEndpoints = [..additionalSentinels],
            AbortOnConnectFail = false
        };
        return this;
    }

    /// <summary>
    /// Configure for Redis Cluster horizontal scaling
    /// </summary>
    /// <param name="initialNode">Initial cluster node host</param>
    /// <param name="port">Cluster node port (default: 6379)</param>
    /// <param name="password">Redis password (optional)</param>
    /// <param name="additionalNodes">Additional cluster node endpoints (format: host:port)</param>
    public ModuleRedisStateStoreOption UseClusterConnection(
        string initialNode,
        int port = 6379,
        string? password = null,
        params string[] additionalNodes)
    {
        ConnectionType = ERedisConnectionType.Cluster;
        Connection = new RedisConnectionConfiguration
        {
            Host = initialNode,
            Port = port,
            Password = password,
            AdditionalEndpoints = [..additionalNodes],
            AbortOnConnectFail = false,
            ConnectRetry = 3,
            AllowAdmin = true
        };
        return this;
    }
}
