namespace Monica.Configuration.Models;

/// <summary>
/// Represents one override row from a Monica configuration value source.
/// </summary>
public sealed record ConfigurationValueOverride
{
    /// <summary>
    /// Gets the source-owned override identity.
    /// </summary>
    public required string OverrideId { get; init; }

    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the structured logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the final Microsoft configuration path when known.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the source that produced this override.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets whether the payload is a scalar leaf or a container snapshot.
    /// </summary>
    public ConfigurationOverrideGranularity Granularity { get; init; }

    /// <summary>
    /// Gets the override merge state.
    /// </summary>
    public ConfigurationValueState State { get; init; }

    /// <summary>
    /// Gets the stored value payload.
    /// </summary>
    public required ConfigurationStoredValue Value { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Gets the schema version that validated the value.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the last modification time.
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
