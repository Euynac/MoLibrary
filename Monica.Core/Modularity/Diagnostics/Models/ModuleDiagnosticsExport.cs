using System.Collections.Immutable;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Represents a portable, sanitized diagnostics baseline without option values, paths, stack traces, or raw failures.
/// </summary>
public sealed record ModuleDiagnosticsExport
{
    /// <summary>Gets the portable schema version.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Gets the source composition identity.</summary>
    public required string CompositionId { get; init; }

    /// <summary>Gets the source snapshot revision.</summary>
    public long Revision { get; init; }

    /// <summary>Gets when this export was created.</summary>
    public DateTimeOffset ExportedAtUtc { get; init; }

    /// <summary>Gets whether the exported observation was terminal.</summary>
    public bool IsFinal { get; init; }

    /// <summary>Gets the terminal outcome when available.</summary>
    public ModuleCompositionOutcome? Outcome { get; init; }

    /// <summary>Gets the source application's display name.</summary>
    public string? ApplicationName { get; init; }

    /// <summary>Gets the source application's version.</summary>
    public string? ApplicationVersion { get; init; }

    /// <summary>Gets timing and count totals.</summary>
    public required ModuleDiagnosticsSummary Summary { get; init; }

    /// <summary>Gets sanitized type-discovery statistics and query contributions.</summary>
    public required ModuleDiagnosticsExportTypeDiscovery TypeDiscovery { get; init; }

    /// <summary>Gets sanitized modules keyed by the portable assembly-qualified module identity.</summary>
    public required ImmutableArray<ModuleDiagnosticsExportModule> Modules { get; init; }

    /// <summary>Gets direct dependency edges using portable module identities.</summary>
    public required ImmutableArray<ModuleDiagnosticsExportDependencyEdge> Edges { get; init; }

    /// <summary>Gets sanitized timeline spans used for baseline overlays.</summary>
    public required ImmutableArray<ModuleDiagnosticsExportTraceSpan> TraceSpans { get; init; }

    /// <summary>Gets sanitized structured findings.</summary>
    public required ImmutableArray<ModuleDiagnosticsExportFinding> Findings { get; init; }
}

/// <summary>Contains export-safe type-discovery data.</summary>
public sealed record ModuleDiagnosticsExportTypeDiscovery
{
    /// <summary>Gets bounded discovery counters.</summary>
    public TypeDiscoveryStatistics Statistics { get; init; } = new();

    /// <summary>Gets typed stage durations.</summary>
    public ImmutableArray<TypeDiscoveryStageMetric> Stages { get; init; } = [];

    /// <summary>Gets query summaries with portable module identities.</summary>
    public ImmutableArray<ModuleDiagnosticsExportQuerySummary> Queries { get; init; } = [];
}

/// <summary>Summarizes one exported type query.</summary>
public sealed record ModuleDiagnosticsExportQuerySummary
{
    /// <summary>Gets the stable query identity.</summary>
    public required string QueryId { get; init; }

    /// <summary>Gets portable identities for consuming modules.</summary>
    public ImmutableArray<string> ConsumerModuleIds { get; init; } = [];

    /// <summary>Gets the matched type count.</summary>
    public int MatchCount { get; init; }
}

/// <summary>Contains one export-safe module record.</summary>
public sealed record ModuleDiagnosticsExportModule
{
    /// <summary>Gets the stable assembly-qualified <see cref="ModuleKey.Id"/> identity.</summary>
    public required string ModuleId { get; init; }

    /// <summary>Gets the module's short type name.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the defining assembly's simple name.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>Gets the registration order when active.</summary>
    public int? RegistrationOrder { get; init; }

    /// <summary>Gets whether the module participated in the executable graph.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets its longest dependency-path depth.</summary>
    public int DependencyDepth { get; init; }

    /// <summary>Gets aggregate serial callback duration in milliseconds.</summary>
    public double SerialCallbackDurationMs { get; init; }

    /// <summary>Gets aggregate startup-work execution duration in milliseconds.</summary>
    public double StartupWorkDurationMs { get; init; }
}

/// <summary>Contains one export-safe direct dependency edge.</summary>
public sealed record ModuleDiagnosticsExportDependencyEdge
{
    /// <summary>Gets the requiring module identity.</summary>
    public required string SourceModuleId { get; init; }

    /// <summary>Gets the required module identity.</summary>
    public required string TargetModuleId { get; init; }
}

/// <summary>Contains one export-safe timeline span.</summary>
public sealed record ModuleDiagnosticsExportTraceSpan
{
    /// <summary>Gets the stable span identity.</summary>
    public required string SpanId { get; init; }

    /// <summary>Gets the span category.</summary>
    public ModuleDiagnosticsTraceSpanKind Kind { get; init; }

    /// <summary>Gets the owning portable module identity.</summary>
    public string? ModuleId { get; init; }

    /// <summary>Gets the typed framework stage.</summary>
    public ModuleSystemStage? SystemStage { get; init; }

    /// <summary>Gets the module phase.</summary>
    public ModulePhase? ModulePhase { get; init; }

    /// <summary>Gets the callback kind.</summary>
    public ModuleCallbackKind? CallbackKind { get; init; }

    /// <summary>Gets the span start offset in milliseconds.</summary>
    public double StartedOffsetMs { get; init; }

    /// <summary>Gets the span end offset in milliseconds.</summary>
    public double EndedOffsetMs { get; init; }
}

/// <summary>Contains one export-safe structured finding.</summary>
public sealed record ModuleDiagnosticsExportFinding
{
    /// <summary>Gets the stable finding code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the finding severity.</summary>
    public ModuleDiagnosticFindingSeverity Severity { get; init; }

    /// <summary>Gets typed evidence.</summary>
    public required ModuleDiagnosticFindingEvidence Evidence { get; init; }

    /// <summary>Gets related portable module identities.</summary>
    public ImmutableArray<string> RelatedModuleIds { get; init; } = [];

    /// <summary>Gets related trace spans.</summary>
    public ImmutableArray<string> RelatedSpanIds { get; init; } = [];
}
