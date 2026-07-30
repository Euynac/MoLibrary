using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes the remaining time for one work item that was pending at checkpoint entry.
/// </summary>
public sealed class ModuleCompositionCheckpointPendingWorkInfo
{
    /// <summary>Gets the referenced composition work identity.</summary>
    public string WorkItemId { get; init; } = string.Empty;

    /// <summary>Gets the monotonic duration from checkpoint entry until this work completed.</summary>
    public double RemainingDurationMs { get; init; }
}

/// <summary>
/// Describes the serial barrier reached at one composition-work checkpoint.
/// </summary>
public sealed class ModuleCompositionCheckpointPerformanceInfo
{
    /// <summary>Gets the checkpoint occurrence sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the checkpoint reached by the serial composition pipeline.</summary>
    public ModuleCompositionWorkDeadline Deadline { get; init; }

    /// <summary>Gets the UTC timestamp at checkpoint entry.</summary>
    public DateTimeOffset EnteredAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp when the serial pipeline was released.</summary>
    public DateTimeOffset ReleasedAtUtc { get; init; }

    /// <summary>Gets the monotonic checkpoint-entry offset from composition origin, in milliseconds.</summary>
    public double EnteredOffsetMs { get; init; }

    /// <summary>Gets the monotonic checkpoint-release offset from composition origin, in milliseconds.</summary>
    public double ReleasedOffsetMs { get; init; }

    /// <summary>Gets every work item governed and first reported by this checkpoint.</summary>
    public IReadOnlyList<string> DueWorkItemIds { get; init; } = [];

    /// <summary>Gets the work items incomplete at checkpoint entry and their exact remaining durations.</summary>
    public IReadOnlyList<ModuleCompositionCheckpointPendingWorkInfo> PendingWorkItems { get; init; } = [];

    /// <summary>Gets the pending work item whose completion released the barrier, or <see langword="null"/>.</summary>
    public string? ReleasingWorkItemId { get; init; }

    /// <summary>Gets the serial wait imposed by this checkpoint, in milliseconds.</summary>
    public double WaitDurationMs => Math.Max(0, ReleasedOffsetMs - EnteredOffsetMs);

    /// <summary>
    /// Gets the startup-blocking portion of the checkpoint interval. A checkpoint with no pending work contributes
    /// no blocking time even though entering and releasing the barrier has measurable bookkeeping overhead.
    /// </summary>
    public double BlockingWaitDurationMs => PendingWorkItemCount == 0 ? 0 : WaitDurationMs;

    /// <summary>Gets the number of work items governed and first reported by this checkpoint.</summary>
    public int WorkItemCount => DueWorkItemIds.Count;

    /// <summary>Gets the number of work items that were incomplete at checkpoint entry.</summary>
    public int PendingWorkItemCount => PendingWorkItems.Count;
}
