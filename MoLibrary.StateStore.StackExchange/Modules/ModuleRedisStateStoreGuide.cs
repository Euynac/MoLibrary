using MoLibrary.Core.Module.Interfaces;
using MoLibrary.StateStore.Modules;
using MoLibrary.StateStore.StackExchange.Connection;

namespace MoLibrary.StateStore.StackExchange.Modules;

public class ModuleRedisStateStoreGuide : MoModuleGuide<ModuleRedisStateStore, ModuleRedisStateStoreOption, ModuleRedisStateStoreGuide>
{
    public ModuleRedisStateStoreGuide()
    {
        DependsOnModule<ModuleStateStoreGuide>().Register();
    }

    /// <summary>
    /// Configure for simple single-node Redis connection
    /// </summary>
    /// <param name="host">Redis host (default: localhost)</param>
    /// <param name="port">Redis port (default: 6379)</param>
    /// <param name="password">Redis password (optional)</param>
    public ModuleRedisStateStoreGuide UseNormalConnection(
        string host = "localhost",
        int port = 6379,
        string? password = null)
    {
        return ConfigureModuleOption(opt =>
        {
            opt.ConnectionType = ERedisConnectionType.Normal;
            opt.Connection = new RedisConnectionConfiguration
            {
                Host = host,
                Port = port,
                Password = password
            };
        });
    }

    /// <summary>
    /// Configure for Redis Sentinel high availability
    /// </summary>
    /// <param name="sentinelHost">Primary Sentinel host</param>
    /// <param name="sentinelPort">Sentinel port (default: 26379)</param>
    /// <param name="serviceName">Master service name (default: mymaster)</param>
    /// <param name="password">Redis password (optional)</param>
    /// <param name="additionalSentinels">Additional sentinel endpoints (format: host:port)</param>
    public ModuleRedisStateStoreGuide UseSentinelConnection(
        string sentinelHost,
        int sentinelPort = 26379,
        string serviceName = "mymaster",
        string? password = null,
        params string[] additionalSentinels)
    {
        return ConfigureModuleOption(opt =>
        {
            opt.ConnectionType = ERedisConnectionType.Sentinel;
            opt.Connection = new RedisConnectionConfiguration
            {
                Host = sentinelHost,
                Port = sentinelPort,
                ServiceName = serviceName,
                Password = password,
                AdditionalEndpoints = [..additionalSentinels],
                AbortOnConnectFail = false
            };
        });
    }

    /// <summary>
    /// Configure for Redis Cluster horizontal scaling
    /// </summary>
    /// <param name="initialNode">Initial cluster node host</param>
    /// <param name="port">Cluster node port (default: 6379)</param>
    /// <param name="password">Redis password (optional)</param>
    /// <param name="additionalNodes">Additional cluster node endpoints (format: host:port)</param>
    public ModuleRedisStateStoreGuide UseClusterConnection(
        string initialNode,
        int port = 6379,
        string? password = null,
        params string[] additionalNodes)
    {
        return ConfigureModuleOption(opt =>
        {
            opt.ConnectionType = ERedisConnectionType.Cluster;
            opt.Connection = new RedisConnectionConfiguration
            {
                Host = initialNode,
                Port = port,
                Password = password,
                AdditionalEndpoints = [..additionalNodes],
                AbortOnConnectFail = false,
                ConnectRetry = 3,
                AllowAdmin = true
            };
        });
    }
}
