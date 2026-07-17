namespace Monica.Configuration.Models;

/// <summary>
/// Identifies why persisted configuration definition metadata cannot be materialized safely.
/// </summary>
public enum ConfigurationDefinitionMetadataIssueKind
{
    /// <summary>
    /// The persisted metadata envelope is missing required fields or contains invalid values.
    /// </summary>
    InvalidEnvelope,

    /// <summary>
    /// The persisted reload behavior is not recognized by the current runtime.
    /// </summary>
    InvalidReloadBehavior,

    /// <summary>
    /// The persisted schema JSON is malformed.
    /// </summary>
    InvalidSchemaJson,

    /// <summary>
    /// The persisted schema payload does not match its published structural hash.
    /// </summary>
    SchemaHashMismatch,

    /// <summary>
    /// The persisted schema was produced by an older contract and must be republished.
    /// </summary>
    OutdatedSchemaContract,

    /// <summary>
    /// The persisted schema is structurally invalid or unsupported.
    /// </summary>
    InvalidSchema
}

/// <summary>
/// Identifies the operator action recommended for invalid definition metadata.
/// </summary>
public enum ConfigurationDefinitionMetadataRepairAction
{
    /// <summary>
    /// The owning project must publish the definition again using a compatible runtime.
    /// </summary>
    RepublishDefinition,

    /// <summary>
    /// An operator must repair conflicting or incorrectly keyed records in the metadata store before republishing.
    /// </summary>
    RepairMetadataStore
}

/// <summary>
/// Describes one persisted configuration definition metadata problem without exposing its raw schema payload.
/// </summary>
public sealed record ConfigurationDefinitionMetadataDiagnostic
{
    /// <summary>
    /// Gets the stable diagnostic category.
    /// </summary>
    public ConfigurationDefinitionMetadataIssueKind Kind { get; init; }

    /// <summary>
    /// Gets the metadata store that supplied the invalid record.
    /// </summary>
    public required string StoreKey { get; init; }

    /// <summary>
    /// Gets the exception type or validation category that produced this diagnostic.
    /// </summary>
    public required string ErrorType { get; init; }

    /// <summary>
    /// Gets the complete technical message suitable for operator diagnostics.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the schema path associated with the problem, when one is known.
    /// </summary>
    public string? SchemaPath { get; init; }

    /// <summary>
    /// Gets the JSON payload path associated with a syntax or shape problem, when one is known.
    /// </summary>
    public string? JsonPath { get; init; }

    /// <summary>
    /// Gets the recommended repair action.
    /// </summary>
    public ConfigurationDefinitionMetadataRepairAction RecommendedAction { get; init; } =
        ConfigurationDefinitionMetadataRepairAction.RepublishDefinition;
}
