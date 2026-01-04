namespace MoLibrary.StateStore.StackExchange.Connection;

/// <summary>
/// Redis connection type enumeration
/// </summary>
public enum ERedisConnectionType
{
    /// <summary>
    /// Standard single-node or simple replica set connection
    /// </summary>
    Normal,

    /// <summary>
    /// Redis Sentinel for high availability with automatic failover
    /// </summary>
    Sentinel,

    /// <summary>
    /// Redis Cluster for horizontal scaling
    /// </summary>
    Cluster
}
