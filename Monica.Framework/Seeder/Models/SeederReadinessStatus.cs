namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Describes how the current Seeder run contributes to application readiness.
/// </summary>
public enum SeederReadinessStatus
{
    /// <summary>No Seeder state currently prevents readiness or reports degradation.</summary>
    Healthy,

    /// <summary>Only unsuccessful optional Seeders contribute operational degradation.</summary>
    Degraded,

    /// <summary>A required Seeder is incomplete or unsuccessful, or FailFast aborted the run.</summary>
    Unhealthy
}
