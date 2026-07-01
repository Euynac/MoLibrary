namespace Monica.Configuration.UI.Models;

/// <summary>
/// Describes the current phase of a configuration package import.
/// </summary>
public enum ConfigurationImportProgressStage
{
    /// <summary>
    /// The uploaded package is being read and parsed.
    /// </summary>
    ReadingPackage,

    /// <summary>
    /// Current runtime configuration definitions are being loaded.
    /// </summary>
    LoadingDefinitions,

    /// <summary>
    /// Imported definitions are being compared with current state.
    /// </summary>
    AnalyzingDefinitions
}

/// <summary>
/// Carries observable progress for a configuration package import.
/// </summary>
public sealed record ConfigurationImportProgress
{
    /// <summary>
    /// Gets the current import stage.
    /// </summary>
    public ConfigurationImportProgressStage Stage { get; init; }

    /// <summary>
    /// Gets the number of current runtime definitions already loaded.
    /// </summary>
    public int LoadedDefinitionCount { get; init; }

    /// <summary>
    /// Gets the total number of current runtime definitions to load.
    /// </summary>
    public int TotalDefinitionCount { get; init; }

    /// <summary>
    /// Gets the number of imported package definitions already analyzed.
    /// </summary>
    public int ProcessedPackageDefinitionCount { get; init; }

    /// <summary>
    /// Gets the total number of imported package definitions to analyze.
    /// </summary>
    public int TotalPackageDefinitionCount { get; init; }

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
    /// Gets a percentage based on definition loading plus package analysis, or <c>null</c> before totals are known.
    /// </summary>
    public double? Percent
    {
        get
        {
            var total = TotalDefinitionCount + TotalPackageDefinitionCount;
            if (total <= 0)
            {
                return null;
            }

            var completed = LoadedDefinitionCount + ProcessedPackageDefinitionCount;
            return Math.Clamp(completed * 100d / total, 0d, 100d);
        }
    }
}
