namespace Monica.Configuration.Models;

/// <summary>
/// Represents the current effective JSON document for one configuration definition.
/// </summary>
public sealed record ConfigurationEffectiveValueDocument
{
    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the effective JSON object graph.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Gets the schema version used when the document was last validated.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets when the document was last modified.
    /// </summary>
    public DateTimeOffset LastModifiedTime { get; init; }

    /// <summary>
    /// Gets the modifier identity.
    /// </summary>
    public string? LastModifierId { get; init; }

    /// <summary>
    /// Gets the modifier display name.
    /// </summary>
    public string? LastModifierName { get; init; }
}
