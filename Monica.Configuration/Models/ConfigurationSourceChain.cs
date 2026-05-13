namespace Monica.Configuration.Models;

/// <summary>
/// Describes source values contributing to one logical configuration path.
/// </summary>
public sealed record ConfigurationSourceChain
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
    /// Gets the projected Microsoft configuration path, when known.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets all source values in priority order.
    /// </summary>
    public required IReadOnlyList<ConfigurationSourceValue> Sources { get; init; }

    /// <summary>
    /// Gets the effective source key.
    /// </summary>
    public string? EffectiveSourceKey { get; init; }
}
