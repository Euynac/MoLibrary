using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.Api;

/// <summary>
/// Query options for the external configuration parameter list API.
/// </summary>
public sealed record ConfigurationParameterQuery
{
    /// <summary>
    /// Gets the optional definition key filter.
    /// </summary>
    public string? DefinitionKey { get; init; }

    /// <summary>
    /// Gets the optional text search term.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Gets whether object, dictionary, and list container nodes are included.
    /// </summary>
    public bool IncludeContainers { get; init; }

    /// <summary>
    /// Gets whether scalar rows include current effective display values.
    /// </summary>
    public bool IncludeEffectiveValue { get; init; } = true;

    /// <summary>
    /// Gets whether scalar rows include effective source summaries.
    /// </summary>
    public bool IncludeSource { get; init; } = true;
}

/// <summary>
/// Parameter list response used by external configuration frontends.
/// </summary>
public sealed record ConfigurationParameterListResponse
{
    /// <summary>
    /// Gets flattened parameter rows.
    /// </summary>
    public IReadOnlyList<ConfigurationParameterRow> Items { get; init; } = [];
}

/// <summary>
/// Flattened configuration parameter row for external frontends.
/// </summary>
public sealed record ConfigurationParameterRow
{
    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the owning definition display name.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the project assembly that published the definition.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets the optional definition category.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the schema version used by this row.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the schema hash used to detect drift.
    /// </summary>
    public required string SchemaHash { get; init; }

    /// <summary>
    /// Gets the canonical logical path.
    /// </summary>
    public required string LogicalPath { get; init; }

    /// <summary>
    /// Gets the projected Microsoft configuration path when known.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the stable schema node key.
    /// </summary>
    public required string NodeKey { get; init; }

    /// <summary>
    /// Gets the display name for the node.
    /// </summary>
    public required string NodeDisplayName { get; init; }

    /// <summary>
    /// Gets the node description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the structural node kind.
    /// </summary>
    public ConfigurationNodeKind NodeKind { get; init; }

    /// <summary>
    /// Gets the scalar value kind when the node is scalar.
    /// </summary>
    public ConfigurationValueKind? ValueKind { get; init; }

    /// <summary>
    /// Gets whether null is valid for the node.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets whether the row represents sensitive data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the effective reload behavior for the row.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; init; }

    /// <summary>
    /// Gets the display-safe current effective value.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets the current Monica effective document version when available.
    /// </summary>
    public long? ValueVersion { get; init; }

    /// <summary>
    /// Gets the source that currently supplies the scalar value.
    /// </summary>
    public ConfigurationSourceDescriptor? EffectiveSource { get; init; }
}

/// <summary>
/// Editable JSON document prepared for an external JSON editor.
/// </summary>
public sealed record ConfigurationJsonEditorDocumentResponse
{
    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the canonical scope path.
    /// </summary>
    public required string ScopePath { get; init; }

    /// <summary>
    /// Gets formatted display-safe JSON text.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// Gets canonical paths redacted in <see cref="Json"/>.
    /// </summary>
    public IReadOnlyList<string> RedactedPaths { get; init; } = [];

    /// <summary>
    /// Gets the schema version used to build the document.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the current effective value version when available.
    /// </summary>
    public long? ValueVersion { get; init; }
}

/// <summary>
/// Request for analyzing an edited JSON snapshot.
/// </summary>
public sealed record ConfigurationJsonDraftAnalyzeRequest
{
    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the canonical scope path. Empty means the definition root.
    /// </summary>
    public string? ScopePath { get; init; }

    /// <summary>
    /// Gets JSON text submitted by the external editor.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// Gets canonical paths that should be skipped because they were redacted.
    /// </summary>
    public IReadOnlyList<string> RedactedPaths { get; init; } = [];

    /// <summary>
    /// Gets whether added keyed dictionary/list items may be compacted into safe container writes.
    /// </summary>
    public bool CompactChanges { get; init; } = true;
}

/// <summary>
/// Result of analyzing an edited JSON snapshot.
/// </summary>
public sealed record ConfigurationJsonDraftAnalyzeResult
{
    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the target definition display name.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the analyzed canonical scope path.
    /// </summary>
    public required string ScopePath { get; init; }

    /// <summary>
    /// Gets saveable changes.
    /// </summary>
    public IReadOnlyList<ConfigurationDraftChange> Changes { get; init; } = [];

    /// <summary>
    /// Gets invalid edits that must block publishing.
    /// </summary>
    public IReadOnlyList<ConfigurationDraftValidationIssue> ValidationIssues { get; init; } = [];

    /// <summary>
    /// Gets non-blocking and blocking diagnostics.
    /// </summary>
    public IReadOnlyList<ConfigurationApiDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the number of unchanged analyzed values.
    /// </summary>
    public int UnchangedCount { get; init; }

    /// <summary>
    /// Gets the number of values skipped because they were redacted.
    /// </summary>
    public int RedactedSkipCount { get; init; }

    /// <summary>
    /// Gets whether the submitted JSON was syntactically valid.
    /// </summary>
    public bool IsJsonValid { get; init; } = true;

    /// <summary>
    /// Gets the JSON parse error when <see cref="IsJsonValid"/> is false.
    /// </summary>
    public string? ParseError { get; init; }

    /// <summary>
    /// Gets whether this draft contains errors that prevent publishing.
    /// </summary>
    public bool HasBlockingIssues =>
        !IsJsonValid ||
        ValidationIssues.Count > 0 ||
        Diagnostics.Any(static diagnostic => diagnostic.Severity == ConfigurationApiDiagnosticSeverity.Error);
}

/// <summary>
/// Saveable draft change produced for an external frontend.
/// </summary>
public sealed record ConfigurationDraftChange
{
    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the target definition display name.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the canonical target logical path.
    /// </summary>
    public required string LogicalPath { get; init; }

    /// <summary>
    /// Gets the target node display name.
    /// </summary>
    public required string NodeDisplayName { get; init; }

    /// <summary>
    /// Gets the UI-facing change classification.
    /// </summary>
    public ConfigurationDraftChangeKind DisplayChangeKind { get; init; }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the storage target kind.
    /// </summary>
    public ConfigurationMutationTargetKind TargetKind { get; init; } = ConfigurationMutationTargetKind.MonicaEffectiveStore;

    /// <summary>
    /// Gets the external source key when <see cref="TargetKind"/> targets an external source.
    /// </summary>
    public string? SourceKey { get; init; }

    /// <summary>
    /// Gets the external source display name when available.
    /// </summary>
    public string? SourceDisplayName { get; init; }

    /// <summary>
    /// Gets the raw JSON value to write for set mutations.
    /// </summary>
    public JsonNode? Value { get; init; }

    /// <summary>
    /// Gets the display-safe old value.
    /// </summary>
    public string? OriginalDisplayValue { get; init; }

    /// <summary>
    /// Gets the display-safe new value.
    /// </summary>
    public string? NewDisplayValue { get; init; }

    /// <summary>
    /// Gets the expected schema version.
    /// </summary>
    public int ExpectedSchemaVersion { get; init; }

    /// <summary>
    /// Gets the expected Monica effective document version.
    /// </summary>
    public long? ExpectedValueVersion { get; init; }

    /// <summary>
    /// Gets the expected external source revision.
    /// </summary>
    public string? ExpectedSourceRevision { get; init; }

    /// <summary>
    /// Gets whether the target node contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the target node kind.
    /// </summary>
    public ConfigurationNodeKind NodeKind { get; init; }

    /// <summary>
    /// Gets the scalar value kind when applicable.
    /// </summary>
    public ConfigurationValueKind? ValueKind { get; init; }

    /// <summary>
    /// Gets the effective reload behavior.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; init; }
}

/// <summary>
/// UI-facing draft change classification.
/// </summary>
public enum ConfigurationDraftChangeKind
{
    /// <summary>
    /// The draft adds a value that did not previously exist.
    /// </summary>
    Added,

    /// <summary>
    /// The draft modifies an existing value.
    /// </summary>
    Modified,

    /// <summary>
    /// The draft removes an existing value.
    /// </summary>
    Removed
}

/// <summary>
/// Invalid draft edit that blocks publishing.
/// </summary>
public sealed record ConfigurationDraftValidationIssue
{
    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the definition display name.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the canonical logical path.
    /// </summary>
    public required string LogicalPath { get; init; }

    /// <summary>
    /// Gets the target node display name.
    /// </summary>
    public required string NodeDisplayName { get; init; }

    /// <summary>
    /// Gets the rejected display value.
    /// </summary>
    public string? InvalidDisplayValue { get; init; }

    /// <summary>
    /// Gets the validation error.
    /// </summary>
    public required string ValidationError { get; init; }

    /// <summary>
    /// Gets whether the value is sensitive.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets validation rules that constrain the node.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationRule> ValidationRules { get; init; } = [];
}

/// <summary>
/// Diagnostic produced by import or JSON draft analysis.
/// </summary>
public sealed record ConfigurationApiDiagnostic
{
    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public ConfigurationApiDiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the related definition key when known.
    /// </summary>
    public string? DefinitionKey { get; init; }

    /// <summary>
    /// Gets the related canonical logical path when known.
    /// </summary>
    public string? LogicalPath { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Severity for external API diagnostics.
/// </summary>
public enum ConfigurationApiDiagnosticSeverity
{
    /// <summary>
    /// Informational diagnostic.
    /// </summary>
    Info,

    /// <summary>
    /// Warning diagnostic.
    /// </summary>
    Warning,

    /// <summary>
    /// Error diagnostic.
    /// </summary>
    Error
}

/// <summary>
/// Request for publishing a group of configuration mutations.
/// </summary>
public sealed record ConfigurationMutationGroupPublishRequest
{
    /// <summary>
    /// Gets the operator-facing group label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the optional publishing reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets mutations to publish in order.
    /// </summary>
    public IReadOnlyList<ConfigurationMutationGroupPublishChange> Changes { get; init; } = [];
}

/// <summary>
/// One change in a mutation group publish request.
/// </summary>
public sealed record ConfigurationMutationGroupPublishChange
{
    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the canonical target logical path.
    /// </summary>
    public string? LogicalPath { get; init; }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the mutation target kind.
    /// </summary>
    public ConfigurationMutationTargetKind TargetKind { get; init; } = ConfigurationMutationTargetKind.MonicaEffectiveStore;

    /// <summary>
    /// Gets the source key for external source mutations.
    /// </summary>
    public string? SourceKey { get; init; }

    /// <summary>
    /// Gets the raw JSON value to write for set mutations.
    /// </summary>
    public JsonNode? Value { get; init; }

    /// <summary>
    /// Gets the expected schema version.
    /// </summary>
    public int ExpectedSchemaVersion { get; init; }

    /// <summary>
    /// Gets the expected Monica effective value version.
    /// </summary>
    public long? ExpectedValueVersion { get; init; }

    /// <summary>
    /// Gets the expected external source revision.
    /// </summary>
    public string? ExpectedSourceRevision { get; init; }
}

/// <summary>
/// Result of publishing a configuration mutation group.
/// </summary>
public sealed record ConfigurationMutationGroupPublishResult
{
    /// <summary>
    /// Gets the publish status.
    /// </summary>
    public ConfigurationMutationGroupPublishStatus Status { get; init; }

    /// <summary>
    /// Gets the mutation group that was created.
    /// </summary>
    public ConfigurationMutationGroup? Group { get; init; }

    /// <summary>
    /// Gets successful mutation results.
    /// </summary>
    public IReadOnlyList<ConfigurationMutationResult> Results { get; init; } = [];

    /// <summary>
    /// Gets the failed change when status is partial or rejected.
    /// </summary>
    public ConfigurationMutationGroupPublishChange? FailedChange { get; init; }

    /// <summary>
    /// Gets the failure message when status is partial or rejected.
    /// </summary>
    public string? FailureMessage { get; init; }
}

/// <summary>
/// Publish status for a mutation group.
/// </summary>
public enum ConfigurationMutationGroupPublishStatus
{
    /// <summary>
    /// All changes were applied.
    /// </summary>
    Applied,

    /// <summary>
    /// Some changes were applied before a later change failed.
    /// </summary>
    PartiallyApplied,

    /// <summary>
    /// No changes were applied.
    /// </summary>
    Rejected
}

/// <summary>
/// Request body for rollback endpoints.
/// </summary>
public sealed record ConfigurationRollbackRequest
{
    /// <summary>
    /// Gets the rollback reason.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Request body for rolling back selected history rows.
/// </summary>
public sealed record ConfigurationRollbackHistoriesRequest
{
    /// <summary>
    /// Gets history row identities to roll back.
    /// </summary>
    public IReadOnlyList<string> HistoryIds { get; init; } = [];

    /// <summary>
    /// Gets the rollback reason.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Request for creating an export package.
/// </summary>
public sealed record ConfigurationApiExportRequest
{
    /// <summary>
    /// Gets the optional single definition key to export.
    /// </summary>
    public string? DefinitionKey { get; init; }

    /// <summary>
    /// Gets whether sensitive values should be included.
    /// </summary>
    public bool IncludeSensitive { get; init; }

    /// <summary>
    /// Gets the operator identity to include in package metadata.
    /// </summary>
    public string? ExportedBy { get; init; }

    /// <summary>
    /// Gets the application version to include in package metadata.
    /// </summary>
    public string? SystemVersion { get; init; }

    /// <summary>
    /// Gets the environment name to include in package metadata.
    /// </summary>
    public string? EnvironmentName { get; init; }
}

/// <summary>
/// Versioned configuration package used by external import and export APIs.
/// </summary>
public sealed record ConfigurationExportDocument
{
    /// <summary>
    /// Gets the export format version.
    /// </summary>
    public int FormatVersion { get; init; } = 1;

    /// <summary>
    /// Gets when the package was exported.
    /// </summary>
    public DateTimeOffset ExportedAt { get; init; }

    /// <summary>
    /// Gets the exporting operator identity.
    /// </summary>
    public string? ExportedBy { get; init; }

    /// <summary>
    /// Gets the exporting application version.
    /// </summary>
    public string? SystemVersion { get; init; }

    /// <summary>
    /// Gets the exporting environment name.
    /// </summary>
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// Gets whether sensitive values were included.
    /// </summary>
    public bool IncludeSensitive { get; init; }

    /// <summary>
    /// Gets exported definitions.
    /// </summary>
    public IReadOnlyList<ConfigurationExportDefinition> Definitions { get; init; } = [];
}

/// <summary>
/// One definition inside a configuration export package.
/// </summary>
public sealed record ConfigurationExportDefinition
{
    /// <summary>
    /// Gets the exported definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the captured display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the captured publishing project assembly name.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets the captured category.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the captured CLR type name.
    /// </summary>
    public required string ClrTypeName { get; init; }

    /// <summary>
    /// Gets the captured schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the captured schema hash.
    /// </summary>
    public string? SchemaHash { get; init; }

    /// <summary>
    /// Gets the Monica effective document version when known.
    /// </summary>
    public long? ValueVersion { get; init; }

    /// <summary>
    /// Gets a summary of sources that supplied exported values.
    /// </summary>
    public IReadOnlyList<string> SourceSummary { get; init; } = [];

    /// <summary>
    /// Gets the exported root JSON value.
    /// </summary>
    public JsonNode? Value { get; init; }

    /// <summary>
    /// Gets redacted canonical logical paths.
    /// </summary>
    public IReadOnlyList<string> RedactedPaths { get; init; } = [];
}

/// <summary>
/// Request for analyzing an import package.
/// </summary>
public sealed record ConfigurationImportAnalyzeRequest
{
    /// <summary>
    /// Gets the uploaded file name.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets the package document to analyze.
    /// </summary>
    public required ConfigurationExportDocument Document { get; init; }

    /// <summary>
    /// Gets whether added keyed dictionary/list items may be compacted into safe container writes.
    /// </summary>
    public bool CompactChanges { get; init; } = true;
}

/// <summary>
/// Aggregated import analysis report.
/// </summary>
public sealed record ConfigurationImportAnalyzeResult
{
    /// <summary>
    /// Gets the uploaded file name.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets package metadata.
    /// </summary>
    public ConfigurationExportDocument? Document { get; init; }

    /// <summary>
    /// Gets per-definition draft analysis results.
    /// </summary>
    public IReadOnlyList<ConfigurationJsonDraftAnalyzeResult> Drafts { get; init; } = [];

    /// <summary>
    /// Gets package-level diagnostics.
    /// </summary>
    public IReadOnlyList<ConfigurationApiDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets all saveable changes across all drafts.
    /// </summary>
    public IReadOnlyList<ConfigurationDraftChange> Changes => Drafts.SelectMany(static draft => draft.Changes).ToArray();

    /// <summary>
    /// Gets all validation issues across all drafts.
    /// </summary>
    public IReadOnlyList<ConfigurationDraftValidationIssue> ValidationIssues =>
        Drafts.SelectMany(static draft => draft.ValidationIssues).ToArray();

    /// <summary>
    /// Gets all diagnostics from package and definition analysis.
    /// </summary>
    public IReadOnlyList<ConfigurationApiDiagnostic> AllDiagnostics =>
        Diagnostics.Concat(Drafts.SelectMany(static draft => draft.Diagnostics)).ToArray();

    /// <summary>
    /// Gets whether import has blocking issues.
    /// </summary>
    public bool HasBlockingIssues =>
        ValidationIssues.Count > 0 ||
        AllDiagnostics.Any(static diagnostic => diagnostic.Severity == ConfigurationApiDiagnosticSeverity.Error);
}

/// <summary>
/// Request for analyzing and publishing an import package.
/// </summary>
public sealed record ConfigurationImportPublishRequest
{
    /// <summary>
    /// Gets the mutation group label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the optional publish reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the uploaded file name.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets the package document to import.
    /// </summary>
    public required ConfigurationExportDocument Document { get; init; }

    /// <summary>
    /// Gets whether added keyed dictionary/list items may be compacted into safe container writes.
    /// </summary>
    public bool CompactChanges { get; init; } = true;

    /// <summary>
    /// Gets whether warning diagnostics may be published without a second confirmation.
    /// </summary>
    public bool AllowWarnings { get; init; }
}

/// <summary>
/// Result of analyzing and publishing an import package.
/// </summary>
public sealed record ConfigurationImportPublishResult
{
    /// <summary>
    /// Gets the import analysis report.
    /// </summary>
    public required ConfigurationImportAnalyzeResult Report { get; init; }

    /// <summary>
    /// Gets the publish result when publishing was attempted.
    /// </summary>
    public ConfigurationMutationGroupPublishResult? PublishResult { get; init; }
}
