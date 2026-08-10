namespace Monica.HealthCheck.Models;

/// <summary>
/// Describes the sanitized state of a health-check snapshot or entry.
/// </summary>
public enum HealthCheckState
{
    /// <summary>
    /// The checked capability is operating normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// The checked capability is available with reduced quality or resilience.
    /// </summary>
    Degraded,

    /// <summary>
    /// The checked capability is unavailable or has not reached readiness.
    /// </summary>
    Unhealthy
}
