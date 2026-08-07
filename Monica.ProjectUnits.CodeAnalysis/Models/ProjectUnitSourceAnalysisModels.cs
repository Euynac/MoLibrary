using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace Monica.ProjectUnits.CodeAnalysis.Models;

/// <summary>Stable semantic-analysis contract version used by persisted consumers.</summary>
public static class ProjectUnitSourceAnalysisContract
{
    /// <summary>
    /// Current source catalog contract version. Version 3 aligns discovery-control attributes with the runtime catalog.
    /// </summary>
    public const string Version = "monica-project-units-source/v3";

    /// <summary>Architectural roles the current source classifier can produce.</summary>
    public static IReadOnlySet<ProjectUnitSourceType> DiscoverableUnitTypes { get; } = new[]
    {
        ProjectUnitSourceType.ApplicationService,
        ProjectUnitSourceType.CrudApplicationService,
        ProjectUnitSourceType.DomainService,
        ProjectUnitSourceType.Repository,
        ProjectUnitSourceType.DomainEvent,
        ProjectUnitSourceType.DomainEventHandler,
        ProjectUnitSourceType.LocalEventHandler,
        ProjectUnitSourceType.Seeder,
        ProjectUnitSourceType.RecurringJob,
        ProjectUnitSourceType.TriggeredJob,
        ProjectUnitSourceType.HttpApi,
        ProjectUnitSourceType.Entity,
        ProjectUnitSourceType.RequestDto,
        ProjectUnitSourceType.Configuration,
        ProjectUnitSourceType.HostedService
    }.ToFrozenSet();
}

/// <summary>
/// Identifies the architectural role of a ProjectUnit discovered through source analysis.
/// </summary>
/// <remarks>
/// Numeric values are part of the persisted source-analysis contract. New roles must be appended so cached Workflow
/// snapshots remain stable without depending on the runtime ProjectUnits assembly.
/// </remarks>
public enum ProjectUnitSourceType
{
    /// <summary>No architectural role has been assigned.</summary>
    None = 0,

    /// <summary>Application service.</summary>
    ApplicationService = 1,

    /// <summary>CRUD application service that participates in automatic controller generation.</summary>
    CrudApplicationService = 2,

    /// <summary>Domain service.</summary>
    DomainService = 3,

    /// <summary>Repository.</summary>
    Repository = 4,

    /// <summary>Domain event.</summary>
    DomainEvent = 5,

    /// <summary>Distributed domain-event handler.</summary>
    DomainEventHandler = 6,

    /// <summary>Local event handler.</summary>
    LocalEventHandler = 7,

    /// <summary>Startup data seeder.</summary>
    Seeder = 8,

    /// <summary>Recurring scheduled job.</summary>
    RecurringJob = 9,

    /// <summary>Triggered job.</summary>
    TriggeredJob = 10,

    /// <summary>HTTP API.</summary>
    HttpApi = 11,

    /// <summary>gRPC API.</summary>
    GrpcApi = 12,

    /// <summary>State store.</summary>
    StateStore = 13,

    /// <summary>Event bus.</summary>
    EventBus = 14,

    /// <summary>Actor.</summary>
    Actor = 15,

    /// <summary>Entity or aggregate.</summary>
    Entity = 16,

    /// <summary>Request DTO.</summary>
    RequestDto = 17,

    /// <summary>Configuration model.</summary>
    Configuration = 18,

    /// <summary>Host-managed long-running or lifecycle service.</summary>
    HostedService = 19
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
    ProjectUnitSourceType UnitType,
    string Title,
    string? Description,
    string? Owner,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> RequirementIds,
    bool HasExplicitMetadata,
    ProjectUnitSourceLocation Source,
    IReadOnlyList<string> ExecutionPoints,
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
