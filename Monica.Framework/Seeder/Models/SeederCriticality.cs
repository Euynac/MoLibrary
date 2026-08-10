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
    /// Reports a degraded state when the seeder fails without making application readiness unhealthy.
    /// </summary>
    Optional
}
