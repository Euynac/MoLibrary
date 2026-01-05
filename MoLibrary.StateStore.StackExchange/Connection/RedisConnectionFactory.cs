using Microsoft.Extensions.Logging;
using MoLibrary.StateStore.StackExchange.Modules;
using StackExchange.Redis;

namespace MoLibrary.StateStore.StackExchange.Connection;

/// <summary>
/// Factory implementation for creating Redis connections supporting Normal, Sentinel, and Cluster modes
/// </summary>
public class RedisConnectionFactory(ILogger<RedisConnectionFactory> logger) : IRedisConnectionFactory
{
    /// <inheritdoc />
    public IConnectionMultiplexer CreateConnection(ModuleRedisStateStoreOption options)
    {
        var config = options.Connection
            ?? throw new InvalidOperationException("Redis connection configuration is required");

        return options.ConnectionType switch
        {
            ERedisConnectionType.Normal => CreateNormalConnection(config),
            ERedisConnectionType.Sentinel => CreateSentinelConnection(config),
            ERedisConnectionType.Cluster => CreateClusterConnection(config),
            _ => throw new ArgumentOutOfRangeException(nameof(options.ConnectionType), options.ConnectionType, "Unknown Redis connection type")
        };
    }

    /// <inheritdoc />
    public IServer GetPrimaryServer(IConnectionMultiplexer connection)
    {
        var servers = connection.GetServers();

        // Try to find a connected master (not replica)
        var master = servers.FirstOrDefault(s => s.IsConnected && !s.IsReplica);
        if (master != null)
        {
            return master;
        }

        // Fallback to any connected server
        var connected = servers.FirstOrDefault(s => s.IsConnected);
        if (connected != null)
        {
            logger.LogWarning("No master server found, using connected replica: {Endpoint}", connected.EndPoint);
            return connected;
        }

        throw new InvalidOperationException("No connected Redis server available");
    }

    private IConnectionMultiplexer CreateNormalConnection(RedisConnectionConfiguration config)
    {
        logger.LogInformation("Creating normal Redis connection to {Host}:{Port}", config.Host, config.Port);

        var options = BuildBaseOptions(config);
        options.AbortOnConnectFail = true;

        return ConnectionMultiplexer.Connect(options);
    }

    private IConnectionMultiplexer CreateSentinelConnection(RedisConnectionConfiguration config)
    {
        logger.LogInformation("Creating Redis Sentinel connection to {Host}:{Port}, service: {ServiceName}",
            config.Host, config.Port, config.ServiceName);

        // Step 1: Connect to Sentinel
        var sentinelOptions = new ConfigurationOptions
        {
            TieBreaker = "",
            CommandMap = CommandMap.Sentinel,
            AbortOnConnectFail = false
        };

        sentinelOptions.EndPoints.Add(config.Host, config.Port);
        foreach (var endpoint in config.AdditionalEndpoints)
        {
            sentinelOptions.EndPoints.Add(endpoint);
        }

        if (!string.IsNullOrEmpty(config.Password))
        {
            sentinelOptions.Password = config.Password;
        }

        var sentinelConnection = ConnectionMultiplexer.Connect(sentinelOptions);

        // Step 2: Get master connection from Sentinel
        var masterOptions = new ConfigurationOptions
        {
            ServiceName = config.ServiceName,
            AbortOnConnectFail = true,
            ConnectTimeout = config.ConnectTimeout,
            SyncTimeout = config.SyncTimeout
        };

        if (!string.IsNullOrEmpty(config.Password))
        {
            masterOptions.Password = config.Password;
        }

        logger.LogInformation("Connected to Sentinel, resolving master for service: {ServiceName}", config.ServiceName);
        return sentinelConnection.GetSentinelMasterConnection(masterOptions);
    }

    private IConnectionMultiplexer CreateClusterConnection(RedisConnectionConfiguration config)
    {
        logger.LogInformation("Creating Redis Cluster connection starting with {Host}:{Port}", config.Host, config.Port);

        var options = BuildBaseOptions(config);
        options.AbortOnConnectFail = false; // Cluster nodes may change dynamically
        options.ConnectRetry = config.ConnectRetry;
        options.AllowAdmin = config.AllowAdmin;
        options.CommandMap = CommandMap.Default; // Default command map supports clustering

        // Add additional cluster nodes
        foreach (var endpoint in config.AdditionalEndpoints)
        {
            options.EndPoints.Add(endpoint);
        }

        return ConnectionMultiplexer.Connect(options);
    }

    private static ConfigurationOptions BuildBaseOptions(RedisConnectionConfiguration config)
    {
        var options = new ConfigurationOptions
        {
            ConnectTimeout = config.ConnectTimeout,
            SyncTimeout = config.SyncTimeout,
            ConnectRetry = config.ConnectRetry,
            AbortOnConnectFail = config.AbortOnConnectFail,
            AllowAdmin = config.AllowAdmin,
            Ssl = config.Ssl
        };

        options.EndPoints.Add(config.Host, config.Port);

        if (!string.IsNullOrEmpty(config.Password))
        {
            options.Password = config.Password;
        }

        return options;
    }
}
