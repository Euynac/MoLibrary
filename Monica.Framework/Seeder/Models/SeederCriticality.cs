namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Defines whether a seeder contributes to application readiness.
/// </summary>
public enum SeederCriticality
{
    /// <summary>
    /// Uses the host-level default configured by <see cref="Monica.Modules.ModuleSeederOption"/>.
    /// </summary>
    Inherit,

    /// <summary>
    /// Keeps the application unready until the seeder succeeds.
    /// </summary>
    Required,

    /// <summary>
    /// Reports a degraded state for an ordinary failure. A run aborted by an effective
    /// <see cref="SeederFailureBehavior.FailFast"/> policy remains unhealthy regardless of this criticality.
    /// </summary>
    Optional
}
