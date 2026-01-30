namespace Monica.Dapr.Interfaces;

/// <summary>
/// Coordinates Dapr sidecar health checking and provides synchronization for dependent services.
/// Similar pattern to IServiceRegistrationCoordinator.
/// </summary>
public interface IDaprSidecarHealthCoordinator
{
    /// <summary>
    /// Gets the current health status of the Dapr sidecar
    /// </summary>
    DaprHealthStatus Status { get; }

    /// <summary>
    /// Gets whether the Dapr sidecar is healthy and ready for use
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    /// Gets the timestamp of the last successful health check
    /// </summary>
    DateTime? LastHealthyAt { get; }

    /// <summary>
    /// Gets the number of consecutive health check failures
    /// </summary>
    int ConsecutiveFailures { get; }

    /// <summary>
    /// Waits for the Dapr sidecar to become healthy or timeout.
    /// Used by DaprEventBusSubscriptionHostedService to ensure sidecar is ready before subscribing.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for sidecar to become healthy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sidecar became healthy, false if timed out or failed</returns>
    Task<bool> WaitForHealthyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the health status of the Dapr sidecar
/// </summary>
public enum DaprHealthStatus
{
    /// <summary>
    /// Health check has not started yet
    /// </summary>
    NotStarted,

    /// <summary>
    /// Initial health check is in progress
    /// </summary>
    Checking,

    /// <summary>
    /// Dapr sidecar is healthy and ready
    /// </summary>
    Healthy,

    /// <summary>
    /// Dapr sidecar is in degraded state (intermittent failures)
    /// </summary>
    Degraded,

    /// <summary>
    /// Dapr sidecar is unhealthy or unreachable
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Health check failed after all retry attempts
    /// </summary>
    Failed
}
