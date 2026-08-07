using System.Collections.Immutable;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>Defines stable finding codes emitted by the module diagnostics projector.</summary>
public static class ModuleDiagnosticFindingCodes
{
    /// <summary>A host-configured startup performance budget was exceeded.</summary>
    public const string PERFORMANCE_BUDGET_EXCEEDED = "module.performance.budget_exceeded";

    /// <summary>A module registration callback failed.</summary>
    public const string REGISTRATION_FAILED = "module.registration.failed";

    /// <summary>A scheduled startup-work item failed.</summary>
    public const string STARTUP_WORK_FAILED = "module.startup_work.failed";

    /// <summary>A serial startup-work commit failed after its worker completed.</summary>
    public const string STARTUP_WORK_COMMIT_FAILED = "module.startup_work.commit_failed";

    /// <summary>The host-owned module composition ended in a structural failure.</summary>
    public const string COMPOSITION_FAILED = "module.composition.failed";
}

/// <summary>Describes a structured module-system finding without embedding user-facing prose.</summary>
public sealed record ModuleDiagnosticFinding
{
    /// <summary>Gets the stable localization and automation code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the finding severity.</summary>
    public ModuleDiagnosticFindingSeverity Severity { get; init; }

    /// <summary>Gets bounded localization arguments keyed by stable argument names.</summary>
    public ImmutableDictionary<string, string> Arguments { get; init; } =
        ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);

    /// <summary>Gets typed evidence supporting this finding.</summary>
    public required ModuleDiagnosticFindingEvidence Evidence { get; init; }

    /// <summary>Gets related modules in stable registration order.</summary>
    public ImmutableArray<ModuleKey> RelatedModules { get; init; } = [];

    /// <summary>Gets related trace-span identities.</summary>
    public ImmutableArray<string> RelatedSpanIds { get; init; } = [];
}

/// <summary>Defines diagnostic finding severities.</summary>
public enum ModuleDiagnosticFindingSeverity
{
    /// <summary>Context that does not require action.</summary>
    Information,

    /// <summary>A non-fatal condition that may warrant investigation.</summary>
    Warning,

    /// <summary>A condition that caused module composition or startup work to fail.</summary>
    Error
}

/// <summary>Defines stable evidence shapes produced by the diagnostics projector.</summary>
public enum ModuleDiagnosticFindingEvidenceKind
{
    /// <summary>A configured performance budget was exceeded.</summary>
    PerformanceBudget,

    /// <summary>A module registration callback failed.</summary>
    RegistrationFailure,

    /// <summary>A startup-work item failed.</summary>
    StartupWorkFailure,

    /// <summary>A serial startup-work publication callback failed.</summary>
    StartupWorkCommitFailure,

    /// <summary>The host-owned composition lifecycle failed.</summary>
    CompositionFailure
}

/// <summary>Contains typed, bounded evidence for a diagnostic finding.</summary>
public sealed record ModuleDiagnosticFindingEvidence
{
    /// <summary>Gets the evidence shape.</summary>
    public ModuleDiagnosticFindingEvidenceKind Kind { get; init; }

    /// <summary>Gets the performance metric, when this is budget evidence.</summary>
    public ModulePerformanceBudgetKind? PerformanceMetric { get; init; }

    /// <summary>Gets the observed duration in milliseconds, when applicable.</summary>
    public double? ActualDurationMs { get; init; }

    /// <summary>Gets the configured limit in milliseconds, when applicable.</summary>
    public double? LimitDurationMs { get; init; }

    /// <summary>Gets the module lifecycle phase associated with a failure.</summary>
    public ModulePhase? ModulePhase { get; init; }

    /// <summary>Gets a stable registration-error category, when applicable.</summary>
    public string? ErrorKind { get; init; }

    /// <summary>Gets the startup-work identity, when applicable.</summary>
    public string? WorkItemId { get; init; }

    /// <summary>Gets the sanitized composition-failure category, when applicable.</summary>
    public string? CompositionFailureKind { get; init; }
}
