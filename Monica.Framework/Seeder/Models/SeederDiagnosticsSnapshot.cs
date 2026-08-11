namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Provides the complete read-only Seeder diagnostics view for the current host process.
/// </summary>
public sealed record SeederDiagnosticsSnapshot
{
    /// <summary>Gets the effective module configuration.</summary>
    public required SeederConfigurationSnapshot Configuration { get; init; }

    /// <summary>Gets the current host-owned run and its per-seeder execution state.</summary>
    public required SeederStateSnapshot Run { get; init; }
}
