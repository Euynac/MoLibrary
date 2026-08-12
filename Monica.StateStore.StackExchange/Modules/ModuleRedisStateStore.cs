using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
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
    /// <param name="module">The StateStore registration that will use Redis.</param>
    /// <param name="action">Redis state store configuration delegate</param>
    /// <param name="documentProfileName">The declared durable-document profile selected for this store.</param>
    /// <returns>The Redis StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleRedisStateStore, ModuleRedisStateStoreOption> UseRedisStateStoreProvider(
        this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module,
        Action<ModuleRedisStateStoreOption>? action = null,
        string documentProfileName = ModuleStateStoreOption.DURABLE_JSON_PROFILE)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentProfileName);
        module.Configure(options => EnsureDocumentProfileExists(options, documentProfileName));
        module.SetCommonDistributedStateStoreProvider<RedisStateStore>();
        var providerModule = module.Include<ModuleRedisStateStore, ModuleRedisStateStoreOption>(options =>
        {
            action?.Invoke(options);
            options.DocumentProfileName = documentProfileName;
        });

        // Register global IConnectionMultiplexer for common provider
        module.ConfigureStateStoreServices(services =>
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var factory = sp.GetRequiredService<IRedisConnectionFactory>();
                var options = sp.GetRequiredService<IOptions<ModuleRedisStateStoreOption>>().Value;
                return factory.CreateConnection(options);
            });
        });

        return providerModule;
    }

    /// <summary>
    /// Add Redis state store as a keyed StateStore provider
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="serviceKey">Service key to identify this StateStore instance</param>
    /// <param name="configureOptions">Redis state store configuration delegate</param>
    /// <param name="documentProfileName">The declared durable-document profile selected for this store.</param>
    /// <returns>The StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddKeyedRedisStateStore(
        this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module,
        string serviceKey,
        Action<ModuleRedisStateStoreOption> configureOptions,
        string documentProfileName = ModuleStateStoreOption.DURABLE_JSON_PROFILE)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentProfileName);
        module.Configure(options => EnsureDocumentProfileExists(options, documentProfileName));

        var providerModule = module.Include<ModuleRedisStateStore, ModuleRedisStateStoreOption>()
            .ConfigureProfile(serviceKey, options =>
            {
                configureOptions(options);
                options.DocumentProfileName = documentProfileName;
            });
        module.ConfigureStateStoreServices(services =>
        {
            var profile = providerModule.GetProfile(serviceKey);

            // Register keyed IConnectionMultiplexer using factory
            services.AddKeyedSingleton<IConnectionMultiplexer>(serviceKey, (sp, _) =>
            {
                var factory = sp.GetRequiredService<IRedisConnectionFactory>();
                return factory.CreateConnection(profile);
            });

            // Register keyed RedisStateStore
            services.AddKeyedSingleton<IStateStore>(serviceKey, (sp, _) =>
            {
                var keyedConnection = sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
                return ActivatorUtilities.CreateInstance<RedisStateStore>(
                    sp,
                    keyedConnection,
                    Options.Create(profile));
            });
        });

        module.RecordKeyedServiceKey(serviceKey);
        return module;
    }

    private static void EnsureDocumentProfileExists(
        ModuleStateStoreOption options,
        string documentProfileName)
    {
        if (!options.ContainsDocumentProfile(documentProfileName))
        {
            throw new InvalidOperationException(
                $"State document profile '{documentProfileName}' must be declared before a Redis store selects it.");
        }
    }
}

public class ModuleRedisStateStore : MonicaModule<ModuleRedisStateStoreOption>,
      IStateStoreModuleProvider
{

    public override void Describe(ModuleDescriptor module)
    {
        // Depends on StateStore basic module
        module.Require<ModuleStateStore, ModuleStateStoreOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleRedisStateStoreOption> context)
    {
        var services = context.Services;
        // Only register connection factory (infrastructure)
        // IConnectionMultiplexer and IDistributedStateStore are registered in UseRedisStateStoreProvider
        services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
    }

    #region IStateStoreModuleProvider Implementation

    public Type ProvidesFor => typeof(ModuleStateStore);

    public EStateStoreProviderType ProviderType => EStateStoreProviderType.Redis;

    public EStateStoreCapabilities Capabilities =>
        EStateStoreCapabilities.KeyScanning |
        EStateStoreCapabilities.RawStringRetrieval |
        EStateStoreCapabilities.BulkOperations;

    public string DisplayName => "Redis";

    #endregion
}



/// <summary>
/// Redis state store module configuration options
/// </summary>
public class ModuleRedisStateStoreOption : ModuleOptions<ModuleRedisStateStore>
{
    /// <summary>
    /// Gets the immutable state-document profile selected for every typed operation of this logical Redis store.
    /// </summary>
    public string DocumentProfileName { get; internal set; } = ModuleStateStoreOption.DURABLE_JSON_PROFILE;
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
