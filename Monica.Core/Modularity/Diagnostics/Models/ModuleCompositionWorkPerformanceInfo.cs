using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes one isolated composition work item scheduled by a module.
/// </summary>
public sealed class ModuleCompositionWorkPerformanceInfo
{
    /// <summary>Gets the stable work identity used by checkpoint references within this snapshot.</summary>
    public string WorkItemId { get; init; } = string.Empty;

    /// <summary>Gets the global submission sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the owning module key.</summary>
    public ModuleKey ModuleKey { get; init; }

    /// <summary>Gets the owning module's short CLR type name.</summary>
    public string ModuleTypeName { get; init; } = string.Empty;

    /// <summary>Gets the owning module's fully qualified CLR type name.</summary>
    public string ModuleFullTypeName { get; init; } = string.Empty;

    /// <summary>Gets the owning module's dependency-aware registration order.</summary>
    public int ModuleRegistrationOrder { get; init; }

    /// <summary>Gets the stable work name within the owning module.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the serial module phase that scheduled the work.</summary>
    public ModulePhase OriginPhase { get; init; }

    /// <summary>Gets the latest composition checkpoint by which the work had to complete.</summary>
    public ModuleCompositionWorkDeadline Deadline { get; init; }

    /// <summary>Gets the terminal work status.</summary>
    public ModuleCompositionWorkStatus Status { get; init; }

    /// <summary>Gets when the module submitted the work.</summary>
    public DateTimeOffset SubmittedAtUtc { get; init; }

    /// <summary>Gets when a composition worker began executing the work.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets when execution stopped.</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>Gets the monotonic submission offset from composition origin, in milliseconds.</summary>
    public double SubmittedOffsetMs { get; init; }

    /// <summary>Gets the monotonic worker-start offset from composition origin, in milliseconds.</summary>
    public double StartedOffsetMs { get; init; }

    /// <summary>Gets the monotonic completion offset from composition origin, in milliseconds.</summary>
    public double CompletedOffsetMs { get; init; }

    /// <summary>Gets whether this work was still pending when its deadline checkpoint was entered.</summary>
    public bool WasPendingAtDeadline { get; init; }

    /// <summary>Gets how long this work remained after its deadline checkpoint was entered, in milliseconds.</summary>
    public double RemainingAtDeadlineMs { get; init; }

    /// <summary>Gets whether this work was the last pending item whose completion released its deadline checkpoint.</summary>
    public bool IsDeadlineReleaser { get; init; }

    /// <summary>Gets how long the work waited for a composition worker, in milliseconds.</summary>
    public double QueueDurationMs => Math.Max(0, StartedOffsetMs - SubmittedOffsetMs);

    /// <summary>Gets active worker execution time, in milliseconds.</summary>
    public double ExecutionDurationMs => Math.Max(0, CompletedOffsetMs - StartedOffsetMs);

    /// <summary>Gets the recursive exception message when execution failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Defines terminal states for required module composition work.
/// </summary>
public enum ModuleCompositionWorkStatus
{
    /// <summary>The work completed successfully.</summary>
    Succeeded,

    /// <summary>The work failed and Monica aborted composition at its checkpoint.</summary>
    Failed
}
