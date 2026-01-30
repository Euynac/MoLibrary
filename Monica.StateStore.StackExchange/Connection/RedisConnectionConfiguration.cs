namespace Monica.StateStore.StackExchange.Connection;

/// <summary>
/// Redis connection configuration for all connection modes
/// </summary>
public class RedisConnectionConfiguration
{
    /// <summary>
    /// Primary endpoint host (default: localhost)
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Primary endpoint port (default: 6379)
    /// </summary>
    public int Port { get; set; } = 6379;

    /// <summary>
    /// Redis password (optional)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Additional endpoints for Sentinel/Cluster modes (format: "host:port")
    /// </summary>
    public List<string> AdditionalEndpoints { get; set; } = [];

    /// <summary>
    /// Sentinel-specific: master service name (default: "mymaster")
    /// </summary>
    public string ServiceName { get; set; } = "mymaster";

    /// <summary>
    /// Connection timeout in milliseconds (default: 5000)
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Sync operation timeout in milliseconds (default: 5000)
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Number of retry attempts on connection failure (default: 3)
    /// </summary>
    public int ConnectRetry { get; set; } = 3;

    /// <summary>
    /// Whether to abort on connection failure (default: false for resilience)
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// Allow admin commands (required for some cluster operations)
    /// </summary>
    public bool AllowAdmin { get; set; } = false;

    /// <summary>
    /// Enable SSL/TLS connection
    /// </summary>
    public bool Ssl { get; set; } = false;
}
