namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes one system-level serial composition phase execution.
/// </summary>
public sealed class ModuleSystemPhasePerformanceInfo
{
    /// <summary>Gets the stable execution identity within this snapshot.</summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>Gets the phase occurrence sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the profiler phase name.</summary>
    public string PhaseName { get; init; } = string.Empty;

    /// <summary>Gets the UTC start timestamp.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets the UTC completion timestamp.</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>Gets the monotonic start offset from composition origin, in milliseconds.</summary>
    public double StartedOffsetMs { get; init; }

    /// <summary>Gets the monotonic completion offset from composition origin, in milliseconds.</summary>
    public double CompletedOffsetMs { get; init; }

    /// <summary>Gets the monotonic execution duration, in milliseconds.</summary>
    public double DurationMs => Math.Max(0, CompletedOffsetMs - StartedOffsetMs);
}
