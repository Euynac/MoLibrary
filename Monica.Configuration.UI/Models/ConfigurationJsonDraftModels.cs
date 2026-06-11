using Monica.Configuration.Models;
using Monica.Configuration.UI.State;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Request for converting a JSON document into staged configuration state.
/// </summary>
public sealed record ConfigurationJsonDraftRequest
{
    /// <summary>
    /// Gets the definition that owns the edited JSON scope.
    /// </summary>
    public required ConfigurationDefinition Definition { get; init; }

    /// <summary>
    /// Gets the schema node that owns the edited JSON scope.
    /// </summary>
    public required ConfigurationNodeDefinition ScopeNode { get; init; }

    /// <summary>
    /// Gets the current effective JSON snapshot for the scope.
    /// </summary>
    public required ConfigurationEffectiveValue EffectiveValue { get; init; }

    /// <summary>
    /// Gets effective scalar values keyed by logical path. These values carry runtime source metadata.
    /// </summary>
    public IReadOnlyDictionary<LogicalPath, ConfigurationEffectiveValue> ScalarEffectiveValues { get; init; } =
        new Dictionary<LogicalPath, ConfigurationEffectiveValue>();

    /// <summary>
    /// Gets the JSON text to analyze.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// Gets canonical logical paths that should be ignored because the exported value was redacted.
    /// </summary>
    public IReadOnlySet<string> RedactedPaths { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets whether detailed changes should be compacted into safe container writes.
    /// </summary>
    public bool CompactChanges { get; init; } = true;
}

/// <summary>
/// Result of analyzing a JSON document against one configuration schema scope.
/// </summary>
public sealed record ConfigurationJsonDraftResult
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
    /// Gets the analyzed scope path.
    /// </summary>
    public required LogicalPath ScopePath { get; init; }

    /// <summary>
    /// Gets saveable staged changes.
    /// </summary>
    public IReadOnlyList<PendingChange> Changes { get; init; } = [];

    /// <summary>
    /// Gets invalid edits that should be visible but cannot be saved.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationIssue> ValidationIssues { get; init; } = [];

    /// <summary>
    /// Gets non-blocking diagnostics produced during analysis.
    /// </summary>
    public IReadOnlyList<ConfigurationImportDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the number of known values that matched the current effective state.
    /// </summary>
    public int UnchangedCount { get; init; }

    /// <summary>
    /// Gets the number of values skipped because they were redacted.
    /// </summary>
    public int RedactedSkipCount { get; init; }

    /// <summary>
    /// Gets whether the analyzed JSON is syntactically valid.
    /// </summary>
    public bool IsJsonValid { get; init; } = true;

    /// <summary>
    /// Gets the parse error when <see cref="IsJsonValid"/> is false.
    /// </summary>
    public string? ParseError { get; init; }

    /// <summary>
    /// Gets whether there is anything to apply to the UI state store.
    /// </summary>
    public bool HasState => Changes.Count > 0 || ValidationIssues.Count > 0;
}

/// <summary>
/// Aggregated report produced after reading an import package.
/// </summary>
public sealed record ConfigurationImportReport
{
    /// <summary>
    /// Gets the uploaded file name.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets metadata from the uploaded export package.
    /// </summary>
    public ConfigurationExportDocument? Document { get; init; }

    /// <summary>
    /// Gets analyzed definition draft results.
    /// </summary>
    public IReadOnlyList<ConfigurationJsonDraftResult> Drafts { get; init; } = [];

    /// <summary>
    /// Gets package-level diagnostics.
    /// </summary>
    public IReadOnlyList<ConfigurationImportDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets all saveable staged changes across the package.
    /// </summary>
    public IReadOnlyList<PendingChange> Changes => Drafts.SelectMany(draft => draft.Changes).ToArray();

    /// <summary>
    /// Gets all non-saveable validation issues across the package.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationIssue> ValidationIssues =>
        Drafts.SelectMany(draft => draft.ValidationIssues).ToArray();

    /// <summary>
    /// Gets all diagnostics across the package and definition drafts.
    /// </summary>
    public IReadOnlyList<ConfigurationImportDiagnostic> AllDiagnostics =>
        Diagnostics.Concat(Drafts.SelectMany(draft => draft.Diagnostics)).ToArray();

    /// <summary>
    /// Gets whether this report contains state that can be applied to the UI.
    /// </summary>
    public bool HasState => Drafts.Any(draft => draft.HasState);
}

/// <summary>
/// Request for creating a Monica configuration export package.
/// </summary>
public sealed record ConfigurationExportRequest
{
    /// <summary>
    /// Gets the optional single definition key to export. When null, all managed definitions are exported.
    /// </summary>
    public string? DefinitionKey { get; init; }

    /// <summary>
    /// Gets whether sensitive values should be written to the export package.
    /// </summary>
    public bool IncludeSensitive { get; init; }

    /// <summary>
    /// Gets the operator name to include in the package.
    /// </summary>
    public string? ExportedBy { get; init; }

    /// <summary>
    /// Gets the system version to include in the package.
    /// </summary>
    public string? SystemVersion { get; init; }

    /// <summary>
    /// Gets the environment name to include in the package.
    /// </summary>
    public string? EnvironmentName { get; init; }
}

/// <summary>
/// Import or JSON edit diagnostic.
/// </summary>
public sealed record ConfigurationImportDiagnostic
{
    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public ConfigurationImportDiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the related definition key when known.
    /// </summary>
    public string? DefinitionKey { get; init; }

    /// <summary>
    /// Gets the related logical path when known.
    /// </summary>
    public LogicalPath? LogicalPath { get; init; }

    /// <summary>
    /// Gets the operator-facing diagnostic message.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Severity for import and JSON edit diagnostics.
/// </summary>
public enum ConfigurationImportDiagnosticSeverity
{
    Info,
    Warning,
    Error
}
