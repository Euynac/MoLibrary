namespace Monica.Configuration.Models;

/// <summary>
/// Carries the seed document used when an effective configuration value does not exist yet.
/// </summary>
public sealed record ConfigurationEffectiveValueSeed
{
    /// <summary>
    /// Gets the configuration definition that owns the effective value document.
    /// </summary>
    public required ConfigurationDefinition Definition { get; init; }

    /// <summary>
    /// Gets the JSON document used to initialize the effective value when it is missing.
    /// </summary>
    public required string SeedJson { get; init; }
}
