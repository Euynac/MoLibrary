using System.Collections.Immutable;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>Contains the lazy, host-cached assembly inventory used to explain type-discovery scope.</summary>
public sealed record TypeDiscoveryAssemblyInventory
{
    /// <summary>Gets the owning composition identity.</summary>
    public required string CompositionId { get; init; }

    /// <summary>Gets the UTC time at which assembly resolution was analyzed.</summary>
    public DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>Gets whether conventional project-assembly discovery was enabled.</summary>
    public bool UsesDefaultProjectAssemblies { get; init; }

    /// <summary>Gets configured fuzzy include patterns.</summary>
    public ImmutableArray<string> IncludePatterns { get; init; } = [];

    /// <summary>Gets configured fuzzy exclusion patterns.</summary>
    public ImmutableArray<string> ExcludePatterns { get; init; } = [];

    /// <summary>Gets assembly outcomes ordered by severity and assembly name.</summary>
    public ImmutableArray<TypeDiscoveryAssemblyRecord> Assemblies { get; init; } = [];

    /// <summary>Gets the number of fully scanned assemblies.</summary>
    public int ScannedCount { get; init; }

    /// <summary>Gets the number of resolved assemblies for which type enumeration was unnecessary.</summary>
    public int NotScannedCount { get; init; }

    /// <summary>Gets the number of assemblies excluded from the final scan set.</summary>
    public int ExcludedCount { get; init; }

    /// <summary>Gets the number of assemblies that could not be resolved.</summary>
    public int ResolutionFailedCount { get; init; }

    /// <summary>Gets the number of scanned assemblies with partial type-load failures.</summary>
    public int PartialTypeLoadCount { get; init; }
}

/// <summary>Describes one assembly-resolution or type-scan outcome.</summary>
public sealed record TypeDiscoveryAssemblyRecord
{
    /// <summary>Gets the simple assembly name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the visible assembly version.</summary>
    public string? Version { get; init; }

    /// <summary>Gets the assembly location when available in the live diagnostics boundary.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the factual assembly outcome.</summary>
    public TypeDiscoveryAssemblyOutcome Outcome { get; init; }

    /// <summary>Gets whether this is the entry assembly.</summary>
    public bool IsEntryAssembly { get; init; }

    /// <summary>Gets a bounded failure description for live local inspection.</summary>
    public string? Failure { get; init; }
}

/// <summary>Defines factual outcomes for an assembly considered by type discovery.</summary>
public enum TypeDiscoveryAssemblyOutcome
{
    /// <summary>The assembly was resolved and all available types were scanned.</summary>
    Scanned,

    /// <summary>The assembly was resolved, but no non-empty discovery plan required type enumeration.</summary>
    ResolvedNotScanned,

    /// <summary>The assembly was intentionally omitted from the final scan set.</summary>
    Excluded,

    /// <summary>The assembly could not be resolved for scanning.</summary>
    ResolutionFailed,

    /// <summary>The assembly was scanned but one or more types could not be loaded.</summary>
    PartialTypeLoad
}
