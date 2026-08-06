using System.Collections.Immutable;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes the complete type-discovery work performed for one module composition.
/// </summary>
public sealed record TypeDiscoveryDiagnostics
{
    /// <summary>Gets the final type-discovery statistics recorded by the compiler and commit pipeline.</summary>
    public TypeDiscoveryStatistics Statistics { get; init; } = new();

    /// <summary>Gets each typed discovery stage in execution order.</summary>
    public ImmutableArray<TypeDiscoveryStageMetric> Stages { get; init; } = [];

    /// <summary>Gets structurally distinct queries in stable compiler order.</summary>
    public ImmutableArray<TypeDiscoveryQuerySummary> Queries { get; init; } = [];
}

/// <summary>
/// Contains bounded counters for one type-discovery compilation and commit.
/// </summary>
public sealed record TypeDiscoveryStatistics
{
    /// <summary>Gets the number of assemblies in the final scan set.</summary>
    public int AssemblyCount { get; init; }

    /// <summary>Gets the number of distinct CLR types enumerated from the scan set.</summary>
    public int EnumeratedTypeCount { get; init; }

    /// <summary>Gets the number of types excluded before query evaluation.</summary>
    public int ExcludedTypeCount { get; init; }

    /// <summary>Gets the number of non-empty module discovery plans.</summary>
    public int PlanCount { get; init; }

    /// <summary>Gets the number of structurally distinct queries evaluated.</summary>
    public int DistinctQueryCount { get; init; }

    /// <summary>Gets the aggregate number of type-query matches across distinct queries.</summary>
    public int MatchCount { get; init; }

    /// <summary>
    /// Gets the number of registration commit callbacks invoked, including callbacks reached before a later callback
    /// failed partway through the commit stage.
    /// </summary>
    public int CommitCallbackCount { get; init; }

    /// <summary>Gets service mutations performed by discovery commit callbacks.</summary>
    public TypeDiscoveryServiceRegistrationStatistics ServiceRegistrations { get; init; } = new();
}

/// <summary>
/// Describes one typed stage interval from the composition timeline.
/// </summary>
public sealed record TypeDiscoveryStageMetric
{
    /// <summary>Gets the stable discovery stage.</summary>
    public required ModuleSystemStage Stage { get; init; }

    /// <summary>Gets the stable trace-span identity for this stage.</summary>
    public required string SpanId { get; init; }

    /// <summary>Gets the stage duration in milliseconds.</summary>
    public double DurationMs { get; init; }
}

/// <summary>
/// Summarizes one structurally distinct type query without retaining reflection objects.
/// </summary>
public sealed record TypeDiscoveryQuerySummary
{
    /// <summary>Gets a stable ordinal identity within the composition.</summary>
    public required string QueryId { get; init; }

    /// <summary>Gets the modules that consume this query in dependency-first order.</summary>
    public ImmutableArray<ModuleKey> ConsumerModules { get; init; } = [];

    /// <summary>Gets the number of business types matched by this query.</summary>
    public int MatchCount { get; init; }
}

/// <summary>
/// Counts the service-writer outcomes produced by type-discovery commits.
/// </summary>
public sealed record TypeDiscoveryServiceRegistrationStatistics
{
    /// <summary>Gets descriptors added under previously absent service identities.</summary>
    public int AddedCount { get; init; }

    /// <summary>Gets existing service identities whose last descriptor was replaced.</summary>
    public int ReplacedCount { get; init; }

    /// <summary>Gets conditional additions skipped because their service identity already existed.</summary>
    public int SkippedCount { get; init; }
}
