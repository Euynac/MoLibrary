using System.Text.Json.Nodes;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Versioned JSON document used to move Monica-managed configuration values between environments.
/// </summary>
public sealed record ConfigurationExportDocument
{
    /// <summary>
    /// Gets the current export format version.
    /// </summary>
    public int FormatVersion { get; init; } = 1;

    /// <summary>
    /// Gets when the export was generated.
    /// </summary>
    public DateTimeOffset ExportedAt { get; init; }

    /// <summary>
    /// Gets the operator identity shown in the export file.
    /// </summary>
    public string? ExportedBy { get; init; }

    /// <summary>
    /// Gets the application version reported by the exporting host.
    /// </summary>
    public string? SystemVersion { get; init; }

    /// <summary>
    /// Gets the environment name reported by the exporting host.
    /// </summary>
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// Gets whether sensitive values were included instead of redacted.
    /// </summary>
    public bool IncludeSensitive { get; init; }

    /// <summary>
    /// Gets exported managed configuration definitions.
    /// </summary>
    public IReadOnlyList<ConfigurationExportDefinition> Definitions { get; init; } = [];
}

/// <summary>
/// One Monica-managed configuration definition inside an export package.
/// </summary>
public sealed record ConfigurationExportDefinition
{
    /// <summary>
    /// Gets the stable Monica configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the display name captured when the export was created.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the publishing project assembly name captured when the export was created.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets the developer-defined category captured when the export was created.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the CLR type name captured when the export was created.
    /// </summary>
    public required string ClrTypeName { get; init; }

    /// <summary>
    /// Gets the exported schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the exported schema hash.
    /// </summary>
    public string? SchemaHash { get; init; }

    /// <summary>
    /// Gets the Monica effective document version when the value came from Monica's store.
    /// </summary>
    public long? ValueVersion { get; init; }

    /// <summary>
    /// Gets a compact summary of runtime sources that supplied effective scalar values.
    /// </summary>
    public IReadOnlyList<string> SourceSummary { get; init; } = [];

    /// <summary>
    /// Gets the exported root JSON value.
    /// </summary>
    public JsonNode? Value { get; init; }

    /// <summary>
    /// Gets canonical logical paths that were redacted and must be skipped on import.
    /// </summary>
    public IReadOnlyList<string> RedactedPaths { get; init; } = [];
}
