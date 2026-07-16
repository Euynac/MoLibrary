namespace Monica.Configuration.Models;

/// <summary>
/// Describes whether a definition can be managed using authoritative persisted metadata.
/// </summary>
public enum ConfigurationDefinitionAvailability
{
    /// <summary>
    /// The definition is complete and safe for inspection and mutation.
    /// </summary>
    Available,

    /// <summary>
    /// A local definition exists, but its authoritative published metadata is unavailable or invalid.
    /// </summary>
    AvailableWithMetadataFault,

    /// <summary>
    /// No complete schema is available for the persisted definition.
    /// </summary>
    Unavailable
}

/// <summary>
/// Identifies a metadata-store-wide failure that prevented a complete diagnostic catalog read.
/// </summary>
public enum ConfigurationMetadataStoreIssueKind
{
    /// <summary>
    /// The metadata store could not be reached.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The current process is not permitted to read the metadata store.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// The physical metadata-store schema is missing or incompatible.
    /// </summary>
    IncompatibleStoreSchema,

    /// <summary>
    /// The metadata store failed for another reason while reading the catalog.
    /// </summary>
    ReadFailed
}

/// <summary>
/// Describes a failure that affects the completeness of the entire published metadata catalog.
/// </summary>
public sealed record ConfigurationMetadataStoreDiagnostic
{
    /// <summary>
    /// Gets the metadata store key.
    /// </summary>
    public required string StoreKey { get; init; }

    /// <summary>
    /// Gets the operator-facing metadata store name.
    /// </summary>
    public required string StoreDisplayName { get; init; }

    /// <summary>
    /// Gets the stable store failure category.
    /// </summary>
    public ConfigurationMetadataStoreIssueKind Kind { get; init; }

    /// <summary>
    /// Gets the exception type that produced the failure.
    /// </summary>
    public required string ErrorType { get; init; }

    /// <summary>
    /// Gets the complete technical message suitable for operator diagnostics.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Provides the diagnostic Configuration UI view of available and unavailable definitions.
/// </summary>
public sealed record ConfigurationDefinitionCatalog
{
    /// <summary>
    /// Gets all known definition summaries, including entries whose persisted schema is unavailable.
    /// </summary>
    public IReadOnlyList<ConfigurationDefinitionSummary> Definitions { get; init; } = [];

    /// <summary>
    /// Gets a metadata-store-wide diagnostic when the published catalog could not be read completely.
    /// </summary>
    public ConfigurationMetadataStoreDiagnostic? StoreDiagnostic { get; init; }

    /// <summary>
    /// Gets whether all known definitions are available and the metadata store was read completely.
    /// </summary>
    public bool IsHealthy =>
        StoreDiagnostic is null
        && Definitions.All(static definition => definition.Availability == ConfigurationDefinitionAvailability.Available);

    /// <summary>
    /// Gets the number of definitions that cannot be managed safely.
    /// </summary>
    public int UnavailableCount =>
        Definitions.Count(static definition => definition.Availability != ConfigurationDefinitionAvailability.Available);
}
