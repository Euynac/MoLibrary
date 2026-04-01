namespace Monica.Dapr.Models;

/// <summary>
/// Represents the health status of the Dapr sidecar.
/// </summary>
public enum DaprHealthStatus
{
    /// <summary>
    /// Health check has not started yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Initial health check is in progress.
    /// </summary>
    Checking,

    /// <summary>
    /// Dapr sidecar is healthy and ready.
    /// </summary>
    Healthy,

    /// <summary>
    /// Dapr sidecar is in degraded state because intermittent failures were observed.
    /// </summary>
    Degraded,

    /// <summary>
    /// Dapr sidecar is unhealthy or unreachable.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Health check failed after all retry attempts.
    /// </summary>
    Failed
}
