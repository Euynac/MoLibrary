namespace Monica.ServiceDiscovery.Models;

public enum ServiceStatus
{
    /// <summary>Running</summary>
    Running,

    /// <summary>Updating</summary>
    Updating,

    /// <summary>Offline</summary>
    Offline,

    /// <summary>abnormal</summary>
    Error,

    /// <summary>Unhealthy (heartbeat timed out but did not reach offline threshold)</summary>
    Unhealthy
}