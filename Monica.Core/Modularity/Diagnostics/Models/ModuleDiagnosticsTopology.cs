using System.Collections.Immutable;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>Represents the acyclic direct-dependency topology compiled for one host.</summary>
public sealed record ModuleDiagnosticsTopology
{
    /// <summary>Gets direct hard-dependency edges only.</summary>
    public ImmutableArray<ModuleDiagnosticsDependencyEdge> Edges { get; init; } = [];

    /// <summary>Gets active modules in dependency-first execution order.</summary>
    public ImmutableArray<ModuleKey> TopologicalOrder { get; init; } = [];

    /// <summary>Gets the longest direct-dependency path in the active graph.</summary>
    public int MaximumDepth { get; init; }
}

/// <summary>Describes one direct hard dependency between modules.</summary>
public sealed record ModuleDiagnosticsDependencyEdge
{
    /// <summary>Gets the module that declares the requirement.</summary>
    public required ModuleKey SourceModule { get; init; }

    /// <summary>Gets the directly required module.</summary>
    public required ModuleKey TargetModule { get; init; }
}
