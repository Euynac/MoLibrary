using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes one serial module callback execution.
/// </summary>
public sealed class ModulePhaseExecutionPerformanceInfo
{
    /// <summary>Gets the stable execution identity within this snapshot.</summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>Gets the global callback occurrence sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the owning module key.</summary>
    public ModuleKey ModuleKey { get; init; }

    /// <summary>Gets the owning module's short CLR type name.</summary>
    public string ModuleTypeName { get; init; } = string.Empty;

    /// <summary>Gets the owning module's fully qualified CLR type name.</summary>
    public string ModuleFullTypeName { get; init; } = string.Empty;

    /// <summary>Gets the owning module's dependency-aware registration order.</summary>
    public int ModuleRegistrationOrder { get; init; }

    /// <summary>Gets the callback phase.</summary>
    public ModulePhase Phase { get; init; }

    /// <summary>Gets the UTC start timestamp.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets the UTC completion timestamp.</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>Gets the monotonic start offset from composition origin, in milliseconds.</summary>
    public double StartedOffsetMs { get; init; }

    /// <summary>Gets the monotonic completion offset from composition origin, in milliseconds.</summary>
    public double CompletedOffsetMs { get; init; }

    /// <summary>Gets the monotonic callback duration, in milliseconds.</summary>
    public double DurationMs => Math.Max(0, CompletedOffsetMs - StartedOffsetMs);
}
