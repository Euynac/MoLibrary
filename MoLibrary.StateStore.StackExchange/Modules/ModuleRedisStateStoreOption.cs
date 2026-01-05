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
}
