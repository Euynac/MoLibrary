namespace Monica.Configuration.Models.Internal;

/// <summary>
/// Represents the effective override selected by the merge engine for one logical path.
/// </summary>
internal sealed record MergedNodeValue
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
    /// Gets the effective override.
    /// </summary>
    public required ConfigurationValueOverride Override { get; init; }

    /// <summary>
    /// Gets the priority of the source that produced <see cref="Override"/>.
    /// Higher values win when multiple logical values project to the same Microsoft configuration key.
    /// </summary>
    public int SourcePriority { get; init; }
}
