using MoLibrary.Core.Module.Interfaces;
using MoLibrary.StateStore.StackExchange.Connection;

namespace MoLibrary.StateStore.StackExchange.Modules;

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
