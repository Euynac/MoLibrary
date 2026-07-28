using System.Text.Json.Serialization;
using Monica.ProjectUnits.Models;

namespace Monica.ProjectUnits.CodeAnalysis.Models;

/// <summary>Stable semantic-analysis contract version used by persisted consumers.</summary>
public static class ProjectUnitSourceAnalysisContract
{
    /// <summary>Current source catalog contract version.</summary>
    public const string Version = "monica-project-units-source/v1";
}

/// <summary>Severity assigned to a source-analysis diagnostic.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectUnitSourceDiagnosticSeverity
{
    /// <summary>Informational analysis context.</summary>
    Information,

    /// <summary>Analysis completed but discovered a condition requiring attention.</summary>
    Warning,

    /// <summary>A project or artifact could not be analyzed reliably.</summary>
    Error
}

/// <summary>Current phase of a source-level ProjectUnit analysis.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectUnitSourceAnalysisStage
{
    /// <summary>The analyzer is validating input and locating MSBuild.</summary>
    Initializing,

    /// <summary>MSBuild projects are being loaded.</summary>
    LoadingProjects,

    /// <summary>Project compilations and declared source types are being analyzed.</summary>
    AnalyzingProjects,

    /// <summary>Cross-unit dependency references are being resolved.</summary>
    ResolvingDependencies,

    /// <summary>The source catalog is complete.</summary>
    Completed
}

/// <summary>Workspace and project input for one source analysis.</summary>
public sealed record ProjectUnitSourceAnalysisRequest(
    string WorkspaceRoot,
    IReadOnlyList<string> ProjectPaths);

/// <summary>Progress reported while loading and analyzing projects.</summary>
public sealed record ProjectUnitSourceAnalysisProgress(
    ProjectUnitSourceAnalysisStage Stage,
    int Completed,
    int Total,
    decimal? Percentage,
    string? CurrentProject,
    string Message);

/// <summary>Source position for one declared ProjectUnit.</summary>
public sealed record ProjectUnitSourceLocation(
    string RelativePath,
    int Line,
    int Column);

/// <summary>Actionable diagnostic produced while loading projects or interpreting ProjectUnit contracts.</summary>
public sealed record ProjectUnitSourceDiagnostic(
    string Code,
    ProjectUnitSourceDiagnosticSeverity Severity,
    string Message,
    string? ProjectPath = null,
    string? SourcePath = null,
    int? Line = null);

/// <summary>A semantically discovered source ProjectUnit and its architecture context.</summary>
public sealed record ProjectUnitSourceUnit(
    string CatalogKey,
    string RuntimeKey,
    string ProjectPath,
    string ProjectName,
    string AssemblyName,
    string Namespace,
    string Name,
    EProjectUnitType UnitType,
    string Title,
    string? Description,
    string? Owner,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> RequirementIds,
    bool HasExplicitMetadata,
    ProjectUnitSourceLocation Source,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> DependedBy,
    IReadOnlyList<ProjectUnitSourceDiagnostic> Diagnostics);

/// <summary>Serializable result of a multi-project semantic ProjectUnit analysis.</summary>
public sealed record ProjectUnitSourceCatalog(
    string ContractVersion,
    int RequestedProjectCount,
    int AnalyzedProjectCount,
    bool IsPartial,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<ProjectUnitSourceUnit> Units,
    IReadOnlyList<ProjectUnitSourceDiagnostic> Diagnostics);
