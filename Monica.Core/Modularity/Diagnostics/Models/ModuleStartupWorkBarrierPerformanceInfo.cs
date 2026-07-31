using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes the remaining time for one work item that was pending at barrier entry.
/// </summary>
public sealed class ModuleStartupWorkBarrierPendingWorkInfo
{
    /// <summary>Gets the referenced startup work identity.</summary>
    public string WorkItemId { get; init; } = string.Empty;

    /// <summary>Gets the monotonic duration from barrier entry until this work completed.</summary>
    public double RemainingDurationMs { get; init; }
}

/// <summary>
/// Describes one startup-work barrier reached by Monica's serial control plane.
/// </summary>
public sealed class ModuleStartupWorkBarrierPerformanceInfo
{
    /// <summary>Gets the barrier occurrence sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the reached startup-work barrier.</summary>
    public ModuleStartupWorkBarrier Barrier { get; init; }

    /// <summary>Gets the UTC timestamp at barrier entry.</summary>
    public DateTimeOffset EnteredAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp when the serial pipeline was released.</summary>
    public DateTimeOffset ReleasedAtUtc { get; init; }

    /// <summary>Gets the monotonic barrier-entry offset from composition origin, in milliseconds.</summary>
    public double EnteredOffsetMs { get; init; }

    /// <summary>Gets the monotonic barrier-release offset from composition origin, in milliseconds.</summary>
    public double ReleasedOffsetMs { get; init; }

    /// <summary>Gets every work item governed and first reported by this barrier.</summary>
    public IReadOnlyList<string> DueWorkItemIds { get; init; } = [];

    /// <summary>Gets the work items incomplete at barrier entry and their exact remaining durations.</summary>
    public IReadOnlyList<ModuleStartupWorkBarrierPendingWorkInfo> PendingWorkItems { get; init; } = [];

    /// <summary>Gets the pending work item whose completion released the barrier, or <see langword="null"/>.</summary>
    public string? ReleasingWorkItemId { get; init; }

    /// <summary>Gets the serial wait imposed by this barrier, in milliseconds.</summary>
    public double WaitDurationMs => Math.Max(0, ReleasedOffsetMs - EnteredOffsetMs);

    /// <summary>
    /// Gets the startup-blocking portion of the barrier interval. A barrier with no pending work contributes
    /// no blocking time even though entering and releasing the barrier has measurable bookkeeping overhead.
    /// </summary>
    public double BlockingWaitDurationMs => PendingWorkItemCount == 0 ? 0 : WaitDurationMs;

    /// <summary>Gets the number of work items governed and first reported by this barrier.</summary>
    public int WorkItemCount => DueWorkItemIds.Count;

    /// <summary>Gets the number of work items that were incomplete at barrier entry.</summary>
    public int PendingWorkItemCount => PendingWorkItems.Count;
}
