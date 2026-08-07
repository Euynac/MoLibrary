using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>Describes one normalized span on the module-composition timeline.</summary>
public sealed record ModuleDiagnosticsTraceSpan
{
    /// <summary>Gets the stable identity within this composition.</summary>
    public required string SpanId { get; init; }

    /// <summary>Gets the span category.</summary>
    public ModuleDiagnosticsTraceSpanKind Kind { get; init; }

    /// <summary>Gets the owning module, when this span is module-owned.</summary>
    public ModuleKey? ModuleKey { get; init; }

    /// <summary>Gets the referenced startup-work identity, when applicable.</summary>
    public string? WorkItemId { get; init; }

    /// <summary>Gets the typed framework stage, when applicable.</summary>
    public ModuleSystemStage? SystemStage { get; init; }

    /// <summary>Gets the module lifecycle phase, when applicable.</summary>
    public ModulePhase? ModulePhase { get; init; }

    /// <summary>Gets the callback responsibility, when this span represents a serial callback.</summary>
    public ModuleCallbackKind? CallbackKind { get; init; }

    /// <summary>Gets the startup-work barrier, when applicable.</summary>
    public ModuleStartupWorkBarrier? Barrier { get; init; }

    /// <summary>Gets a bounded runtime name for untyped host phases or startup work.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the UTC start time.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets the UTC completion time, or <see langword="null"/> while the span is live.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>Gets the monotonic start offset from composition origin in milliseconds.</summary>
    public double StartedOffsetMs { get; init; }

    /// <summary>Gets the monotonic end offset at capture time in milliseconds.</summary>
    public double EndedOffsetMs { get; init; }

    /// <summary>Gets the bounded monotonic span duration in milliseconds.</summary>
    public double DurationMs => Math.Max(0, EndedOffsetMs - StartedOffsetMs);

    /// <summary>Gets startup-work queue time in milliseconds, when applicable.</summary>
    public double? QueueDurationMs { get; init; }

    /// <summary>Gets whether this interval reached a terminal boundary.</summary>
    public bool IsComplete => CompletedAtUtc.HasValue;

    /// <summary>Gets whether this span ended in failure.</summary>
    public bool IsFailed { get; init; }
}

/// <summary>Defines normalized module-composition trace categories.</summary>
public enum ModuleDiagnosticsTraceSpanKind
{
    /// <summary>A framework-owned serial stage.</summary>
    SystemStage,

    /// <summary>A module-owned serial callback.</summary>
    ModuleCallback,

    /// <summary>A bounded startup-work execution.</summary>
    StartupWork,

    /// <summary>A serial startup-work barrier wait.</summary>
    StartupBarrier
}

/// <summary>
/// Identifies one pending work item that causally contributed to a startup barrier's blocking interval.
/// </summary>
public sealed record ModuleBlockingChainSegment
{
    /// <summary>Gets the stable segment identity within this composition.</summary>
    public required string SegmentId { get; init; }

    /// <summary>Gets the trace span for the barrier wait.</summary>
    public required string BarrierSpanId { get; init; }

    /// <summary>Gets the startup-work identity.</summary>
    public required string WorkItemId { get; init; }

    /// <summary>Gets the worker execution span that produced the pending result.</summary>
    public required string WorkSpanId { get; init; }

    /// <summary>Gets the serial publication span for this work item, when one was declared.</summary>
    public string? CommitSpanId { get; init; }

    /// <summary>Gets the module that owns the pending work item.</summary>
    public required ModuleKey ModuleKey { get; init; }

    /// <summary>Gets the reached barrier.</summary>
    public ModuleStartupWorkBarrier Barrier { get; init; }

    /// <summary>Gets how long this work remained incomplete after barrier entry.</summary>
    public double BlockingDurationMs { get; init; }

    /// <summary>Gets serial publication time after barrier release, when this work item declared a commit.</summary>
    public double CommitDurationMs { get; init; }

    /// <summary>Gets whether completing this work released the barrier.</summary>
    public bool IsBarrierReleaser { get; init; }
}
