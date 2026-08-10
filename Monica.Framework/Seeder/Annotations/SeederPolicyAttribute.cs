using Monica.Framework.Seeder.Models;

namespace Monica.Framework.Seeder.Annotations;

/// <summary>
/// Overrides host-level scheduling policy for one seeder.
/// </summary>
/// <remarks>
/// Omitted values inherit from <see cref="Monica.Modules.ModuleSeederOption"/>. Setting
/// <see cref="MaxAttempts"/> above one explicitly enables retry for non-cancellation failures.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SeederPolicyAttribute : Attribute
{
    /// <summary>Gets or sets whether this seeder can overlap other ready seeders.</summary>
    public SeederExecutionMode ExecutionMode { get; set; } = SeederExecutionMode.Inherit;

    /// <summary>Gets or sets whether this seeder contributes to application readiness.</summary>
    public SeederCriticality Criticality { get; set; } = SeederCriticality.Inherit;

    /// <summary>
    /// Gets or sets the maximum execution attempts. Zero inherits the host default; one disables retry.
    /// </summary>
    public int MaxAttempts { get; set; }
}
