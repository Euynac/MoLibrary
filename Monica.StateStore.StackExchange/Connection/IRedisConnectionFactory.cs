using Monica.Modules;
using StackExchange.Redis;

namespace Monica.StateStore.StackExchange.Connection;

/// <summary>
/// Factory interface for creating Redis connections
/// </summary>
public interface IRedisConnectionFactory
{
    /// <summary>
    /// Creates a connection multiplexer based on the provided options
    /// </summary>
    /// <param name="options">Redis state store options</param>
    /// <returns>Redis connection multiplexer</returns>
    IConnectionMultiplexer CreateConnection(ModuleRedisStateStoreOption options);

    /// <summary>
    /// Gets the appropriate primary server for write operations
    /// </summary>
    /// <param name="connection">Redis connection multiplexer</param>
    /// <returns>Primary/master Redis server</returns>
    IServer GetPrimaryServer(IConnectionMultiplexer connection);
}
