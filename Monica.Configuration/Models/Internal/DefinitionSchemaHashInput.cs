namespace Monica.Configuration.Models.Internal;

/// <summary>
/// Stable schema hash input used to fingerprint definitions without runtime-only data.
/// </summary>
internal sealed record DefinitionSchemaHashInput
{
    /// <summary>
    /// Gets the definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the section path.
    /// </summary>
    public required string SectionPath { get; init; }

    /// <summary>
    /// Gets the root node.
    /// </summary>
    public required ConfigurationNodeDefinition Root { get; init; }
}
