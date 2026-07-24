using System.Text.Json.Serialization;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Identifies the Monica host represented by a project-unit dashboard snapshot.
/// </summary>
public sealed class ProjectUnitServiceIdentity
{
    /// <summary>
    /// Gets the application project name.
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Gets the application identifier.
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// Gets the application display name.
    /// </summary>
    public required string AppName { get; init; }

    /// <summary>
    /// Gets the application version when configured.
    /// </summary>
    public string? AppVersion { get; init; }

    /// <summary>
    /// Gets the application domain or subdomain name when configured.
    /// </summary>
    public string? DomainName { get; init; }
}

/// <summary>
/// Identifies an independently measured project-unit context dimension.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectUnitCoverageKind
{
    /// <summary>
    /// Explicit <see cref="Annotations.ProjectUnitMetadataAttribute"/> presence.
    /// </summary>
    Metadata,

    /// <summary>
    /// Metadata or XML documentation description presence.
    /// </summary>
    Description,

    /// <summary>
    /// Explicit metadata owner presence.
    /// </summary>
    Ownership,

    /// <summary>
    /// At least one normalized requirement annotation.
    /// </summary>
    Requirements
}

/// <summary>
/// Reports independent coverage for one project-unit context dimension.
/// </summary>
public sealed class ProjectUnitCoverageMetric
{
    /// <summary>
    /// Gets the measured context dimension.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectUnitCoverageKind Kind { get; init; }

    /// <summary>
    /// Gets the number of units satisfying this dimension.
    /// </summary>
    public int Covered { get; init; }

    /// <summary>
    /// Gets the total discovered unit count used as the denominator.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the coverage percentage, or <see langword="null"/> when the catalog is empty.
    /// </summary>
    public decimal? Percentage { get; init; }
}

/// <summary>
/// Reports the distribution of one architectural project-unit category.
/// </summary>
public sealed class ProjectUnitTypeStatistics
{
    /// <summary>
    /// Gets the architectural category.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; init; }

    /// <summary>
    /// Gets the number of units in this category.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Gets this category's percentage of the discovered catalog.
    /// </summary>
    public decimal Percentage { get; init; }
}

/// <summary>
/// Summarizes catalog dependency topology.
/// </summary>
public sealed class ProjectUnitDependencyStatistics
{
    /// <summary>
    /// Gets the total number of directed dependency edges.
    /// </summary>
    public int EdgeCount { get; init; }

    /// <summary>
    /// Gets the number of units with neither incoming nor outgoing dependencies.
    /// </summary>
    public int IsolatedUnitCount { get; init; }
}

/// <summary>
/// Summarizes catalog diagnostics by severity.
/// </summary>
public sealed class ProjectUnitAlertStatistics
{
    /// <summary>
    /// Gets informational alert count.
    /// </summary>
    public int InformationCount { get; init; }

    /// <summary>
    /// Gets warning alert count.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Gets error alert count.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Gets total alert count.
    /// </summary>
    public int TotalCount => InformationCount + WarningCount + ErrorCount;
}

/// <summary>
/// Identifies context debt or catalog diagnostics for one actionable project unit.
/// </summary>
public sealed class ProjectUnitCoverageGap
{
    /// <summary>
    /// Gets the affected unit summary.
    /// </summary>
    public required ProjectUnitSummary Unit { get; init; }

    /// <summary>
    /// Gets context dimensions that are not covered for this unit.
    /// </summary>
    public IReadOnlyList<ProjectUnitCoverageKind> MissingCoverage { get; init; } = [];
}

/// <summary>
/// Provides one startup-stable status snapshot for the current Monica host's project-unit catalog.
/// </summary>
public sealed class ProjectUnitDashboardSnapshot
{
    /// <summary>
    /// Gets the represented service identity.
    /// </summary>
    public required ProjectUnitServiceIdentity Service { get; init; }

    /// <summary>
    /// Gets the total number of discovered project units.
    /// </summary>
    public int TotalUnits { get; init; }

    /// <summary>
    /// Gets the number of distinct project-unit categories present in the catalog.
    /// </summary>
    public int DistinctUnitTypes { get; init; }

    /// <summary>
    /// Gets independent context coverage metrics.
    /// </summary>
    public IReadOnlyList<ProjectUnitCoverageMetric> Coverage { get; init; } = [];

    /// <summary>
    /// Gets unit counts grouped by architectural category.
    /// </summary>
    public IReadOnlyList<ProjectUnitTypeStatistics> UnitTypes { get; init; } = [];

    /// <summary>
    /// Gets dependency topology statistics.
    /// </summary>
    public required ProjectUnitDependencyStatistics Dependencies { get; init; }

    /// <summary>
    /// Gets catalog alert statistics.
    /// </summary>
    public required ProjectUnitAlertStatistics Alerts { get; init; }

    /// <summary>
    /// Gets units with missing context or explicit catalog diagnostics.
    /// </summary>
    public IReadOnlyList<ProjectUnitCoverageGap> CoverageGaps { get; init; } = [];
}
