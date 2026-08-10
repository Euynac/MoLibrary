namespace Monica.HealthCheck.Models;

/// <summary>
/// Selects the health-check registrations included in a point-in-time snapshot.
/// </summary>
public enum HealthCheckScope
{
    /// <summary>
    /// Includes every health check registered in the current host.
    /// </summary>
    All,

    /// <summary>
    /// Includes registrations tagged for readiness.
    /// </summary>
    Readiness,

    /// <summary>
    /// Includes registrations tagged for liveness.
    /// </summary>
    Liveness
}
