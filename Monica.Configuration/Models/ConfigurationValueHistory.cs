namespace Monica.Configuration.Models;

/// <summary>
/// Represents an audit record for a configuration value mutation.
/// </summary>
public sealed record ConfigurationValueHistory
{
    /// <summary>
    /// Gets the history record identity.
    /// </summary>
    public required string HistoryId { get; init; }

    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the mutated logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the projected Microsoft configuration path, when known.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the target storage kind written by this history row.
    /// </summary>
    public ConfigurationMutationTargetKind TargetKind { get; init; } = ConfigurationMutationTargetKind.MonicaEffectiveStore;

    /// <summary>
    /// Gets the source provider type when this row targets an external source.
    /// </summary>
    public string? SourceProviderType { get; init; }

    /// <summary>
    /// Gets the source display name when this row targets an external source.
    /// </summary>
    public string? SourceDisplayName { get; init; }

    /// <summary>
    /// Gets the physical source path when this row targets a file-backed external source.
    /// </summary>
    public string? SourcePhysicalPath { get; init; }

    /// <summary>
    /// Gets the exact Microsoft configuration path written in the target source.
    /// </summary>
    public string? SourceConfigurationPath { get; init; }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the mutation granularity.
    /// </summary>
    public ConfigurationMutationGranularity Granularity { get; init; }

    /// <summary>
    /// Gets the resulting value state.
    /// </summary>
    public ConfigurationValueState State { get; init; }

    /// <summary>
    /// Gets the new stored value.
    /// </summary>
    public required ConfigurationStoredValue NewValue { get; init; }

    /// <summary>
    /// Gets the old stored value when captured.
    /// </summary>
    public ConfigurationStoredValue? OldValue { get; init; }

    /// <summary>
    /// Gets the resulting optimistic concurrency version.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Gets the external source revision before the write.
    /// </summary>
    public string? SourceRevisionBefore { get; init; }

    /// <summary>
    /// Gets the external source revision after the write.
    /// </summary>
    public string? SourceRevisionAfter { get; init; }

    /// <summary>
    /// Gets the schema version used for validation.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the mutation time.
    /// </summary>
    public DateTimeOffset ModifiedTime { get; init; }

    /// <summary>
    /// Gets the modifier identity.
    /// </summary>
    public string? ModifierId { get; init; }

    /// <summary>
    /// Gets the modifier display name.
    /// </summary>
    public string? ModifierName { get; init; }

    /// <summary>
    /// Gets the optional mutation reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the mutation group identity when this history row was applied as part of a batch.
    /// </summary>
    public string? MutationGroupId { get; init; }
}
