namespace Monica.Configuration.Models;

/// <summary>
/// Describes the current effective value at one logical path.
/// </summary>
public sealed record ConfigurationEffectiveValue
{
    /// <summary>
    /// Gets the definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the projected Microsoft configuration path.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the display-safe effective value.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets the effective source key.
    /// </summary>
    public string? EffectiveSourceKey { get; init; }

    /// <summary>
    /// Gets whether the value is sensitive.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the effective value version when source-backed.
    /// </summary>
    public long? Version { get; init; }
}
