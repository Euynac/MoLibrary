using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes one isolated startup work item scheduled by a module.
/// </summary>
internal sealed class ModuleStartupWorkPerformanceInfo
{
    /// <summary>Gets the stable work identity used by barrier references within this snapshot.</summary>
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

    /// <summary>Gets the startup barrier governing the work.</summary>
    public ModuleStartupWorkBarrier Barrier { get; init; }

    /// <summary>Gets the live or terminal work status.</summary>
    public ModuleStartupWorkStatus Status { get; init; }

    /// <summary>Gets when the module submitted the work.</summary>
    public DateTimeOffset SubmittedAtUtc { get; init; }

    /// <summary>Gets when a startup worker began executing the work, when it has started.</summary>
    public DateTimeOffset? StartedAtUtc { get; init; }

    /// <summary>Gets when execution stopped, when it has completed.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>Gets the monotonic submission offset from composition origin, in milliseconds.</summary>
    public double SubmittedOffsetMs { get; init; }

    /// <summary>Gets the monotonic worker-start offset from composition origin, in milliseconds.</summary>
    public double? StartedOffsetMs { get; init; }

    /// <summary>Gets the monotonic completion offset from composition origin, in milliseconds.</summary>
    public double? CompletedOffsetMs { get; init; }

    /// <summary>Gets whether this work was still pending when its barrier was entered.</summary>
    public bool WasPendingAtBarrier { get; init; }

    /// <summary>Gets how long this work remained after its barrier was entered, in milliseconds.</summary>
    public double RemainingAtBarrierMs { get; init; }

    /// <summary>Gets whether this work was the last pending item whose completion released its barrier.</summary>
    public bool IsBarrierReleaser { get; init; }

    /// <summary>Gets how long the work waited for a startup worker, in milliseconds.</summary>
    public double QueueDurationMs { get; init; }

    /// <summary>Gets active worker execution time, in milliseconds.</summary>
    public double ExecutionDurationMs { get; init; }

    /// <summary>Gets the recursive exception message when execution failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Defines live and terminal states for module startup work.
/// </summary>
internal enum ModuleStartupWorkStatus
{
    /// <summary>The work is waiting for a bounded worker.</summary>
    Queued,

    /// <summary>The work is currently executing.</summary>
    Running,

    /// <summary>The work completed successfully.</summary>
    Succeeded,

    /// <summary>The work failed. Required barriers fail startup; non-blocking work remains diagnostic-only.</summary>
    Failed
}
