namespace Monica.Configuration.Models;

/// <summary>
/// Represents a persisted unified configuration version snapshot.
/// </summary>
public sealed record ConfigurationUnifiedVersionSnapshot
{
    /// <summary>
    /// Gets the version summary.
    /// </summary>
    public required ConfigurationUnifiedVersionSummary Summary { get; init; }

    /// <summary>
    /// Gets the captured definition documents.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionDefinitionSnapshot> Definitions { get; init; } = [];
}

/// <summary>
/// Represents the captured effective value of one configuration definition.
/// </summary>
public sealed record ConfigurationUnifiedVersionDefinitionSnapshot
{
    /// <summary>
    /// Gets the definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the definition display name at capture time.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the definition category at capture time.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the project that published or scanned the definition.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets the schema version at capture time.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the schema hash at capture time.
    /// </summary>
    public required string SchemaHash { get; init; }

    /// <summary>
    /// Gets the Monica effective-store document version observed during capture, when available.
    /// </summary>
    public long? EffectiveValueVersion { get; init; }

    /// <summary>
    /// Gets the complete effective JSON value for the definition.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// Gets source contribution metadata captured for display and diagnostics.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionSourceContribution> SourceContributions { get; init; } = [];
}

/// <summary>
/// Describes one source's contribution to a captured definition.
/// </summary>
public sealed record ConfigurationUnifiedVersionSourceContribution
{
    /// <summary>
    /// Gets the source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the source display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the source kind.
    /// </summary>
    public ConfigurationSourceKind Kind { get; init; }

    /// <summary>
    /// Gets the number of values supplied by this source.
    /// </summary>
    public int SuppliedValueCount { get; init; }

    /// <summary>
    /// Gets the number of values for which this source wins.
    /// </summary>
    public int EffectiveValueCount { get; init; }
}
