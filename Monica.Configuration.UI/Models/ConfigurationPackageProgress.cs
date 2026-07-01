namespace Monica.Configuration.UI.Models;

/// <summary>
/// Describes which configuration package operation is reporting progress.
/// </summary>
public enum ConfigurationPackageProgressOperation
{
    /// <summary>
    /// A configuration package is being imported.
    /// </summary>
    Import,

    /// <summary>
    /// A configuration package is being exported.
    /// </summary>
    Export
}

/// <summary>
/// Describes the current phase of a configuration package operation.
/// </summary>
public enum ConfigurationPackageProgressStage
{
    /// <summary>
    /// The uploaded import package is being read and parsed.
    /// </summary>
    ReadingPackage,

    /// <summary>
    /// Current runtime configuration definitions are being loaded.
    /// </summary>
    LoadingDefinitions,

    /// <summary>
    /// Imported definitions are being compared with current state.
    /// </summary>
    AnalyzingDefinitions,

    /// <summary>
    /// Current runtime configuration definitions are being written into an export package.
    /// </summary>
    ExportingDefinitions
}

/// <summary>
/// Carries observable progress for a configuration package import or export.
/// </summary>
public sealed record ConfigurationPackageProgress
{
    /// <summary>
    /// Gets the operation that is reporting progress.
    /// </summary>
    public ConfigurationPackageProgressOperation Operation { get; init; }

    /// <summary>
    /// Gets the current import stage.
    /// </summary>
    public ConfigurationPackageProgressStage Stage { get; init; }

    /// <summary>
    /// Gets the number of current runtime definitions already loaded.
    /// </summary>
    public int LoadedDefinitionCount { get; init; }

    /// <summary>
    /// Gets the total number of current runtime definitions to load.
    /// </summary>
    public int TotalDefinitionCount { get; init; }

    /// <summary>
    /// Gets the number of operation definitions already processed.
    /// </summary>
    public int ProcessedDefinitionCount { get; init; }

    /// <summary>
    /// Gets the total number of operation definitions to process.
    /// </summary>
    public int TotalProcessDefinitionCount { get; init; }

    /// <summary>
    /// Gets the display name of the definition currently being processed, when known.
    /// </summary>
    public string? CurrentDefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the definition key currently being processed, when known.
    /// </summary>
    public string? CurrentDefinitionKey { get; init; }

    /// <summary>
    /// Gets the number of staged changes discovered so far.
    /// </summary>
    public int ChangeCount { get; init; }

    /// <summary>
    /// Gets the number of validation issues discovered so far.
    /// </summary>
    public int ValidationIssueCount { get; init; }

    /// <summary>
    /// Gets the number of diagnostics discovered so far.
    /// </summary>
    public int DiagnosticCount { get; init; }

    /// <summary>
    /// Gets the number of sensitive value paths redacted during export.
    /// </summary>
    public int RedactedPathCount { get; init; }

    /// <summary>
    /// Gets a percentage based on definition loading plus package analysis, or <c>null</c> before totals are known.
    /// </summary>
    public double? Percent
    {
        get
        {
            var total = TotalDefinitionCount + TotalProcessDefinitionCount;
            if (total <= 0)
            {
                return null;
            }

            var completed = LoadedDefinitionCount + ProcessedDefinitionCount;
            return Math.Clamp(completed * 100d / total, 0d, 100d);
        }
    }
}
