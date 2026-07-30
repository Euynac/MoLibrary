using Monica.Core.Modularity.Models;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Presents one immutable, UI-focused projection of module composition diagnostics.
/// </summary>
public sealed record ModulePerformanceDashboardView(
    ModulePerformanceSummaryView Summary,
    ModuleCriticalPathView? CriticalPath,
    IReadOnlyList<ModuleCompositionMilestoneView> Milestones,
    IReadOnlyList<ModuleCompositionCheckpointView> Checkpoints,
    IReadOnlyList<ModuleCompositionWorkItemView> WorkItems,
    IReadOnlyList<ModuleSerialCallbackView> SerialModules,
    IReadOnlyList<ModuleSystemPhaseView> SystemPhases);

/// <summary>
/// Contains the non-additive headline dimensions of module composition performance.
/// </summary>
public sealed record ModulePerformanceSummaryView(
    double EndToEndElapsedMs,
    double AggregateSerialCallbackMs,
    int SerialCallbackCount,
    double StartupBlockingWaitMs,
    double ParallelWorkActiveSpanMs,
    double AggregateWorkerExecutionMs,
    double AggregateWorkerQueueMs,
    int WorkItemCount);

/// <summary>
/// Identifies the parallel work item that released the longest startup-blocking checkpoint.
/// </summary>
public sealed record ModuleCriticalPathView(
    string Checkpoint,
    string ReleasingModuleTypeName,
    ModuleKey? ReleasingModuleKey,
    string ReleasingWorkName,
    double WaitDurationMs);

/// <summary>
/// Represents one composition milestone measured from the shared monotonic origin.
/// </summary>
public sealed record ModuleCompositionMilestoneView(
    long Sequence,
    string Name,
    double OffsetMs,
    double ElapsedSincePreviousMs,
    bool HasPreviousMilestone,
    bool IsHostOwnedGap);

/// <summary>
/// Represents one parallel-work deadline reached by the serial composition pipeline.
/// </summary>
public sealed record ModuleCompositionCheckpointView(
    long Sequence,
    string Deadline,
    double EnteredOffsetMs,
    double ReleasedOffsetMs,
    double WaitDurationMs,
    IReadOnlyList<string> DueWorkItemNames,
    IReadOnlyList<ModuleCompositionCheckpointPendingWorkView> PendingWorkItems,
    string? ReleasingWorkItemName);

/// <summary>
/// Represents work that was still running when a composition barrier was reached.
/// </summary>
public sealed record ModuleCompositionCheckpointPendingWorkView(
    string WorkItemName,
    double RemainingDurationMs);

/// <summary>
/// Represents one module-owned parallel composition work item.
/// </summary>
public sealed record ModuleCompositionWorkItemView(
    long Sequence,
    int ModuleOrder,
    string ModuleTypeName,
    ModuleKey? ModuleKey,
    string Name,
    string OriginPhase,
    string Deadline,
    string Status,
    double SubmittedOffsetMs,
    double QueueDurationMs,
    double ExecutionDurationMs,
    bool WasPendingAtDeadline,
    double? RemainingAtDeadlineMs,
    string? ErrorMessage);

/// <summary>
/// Summarizes serial callbacks for one module without mixing in parallel work duration.
/// </summary>
public sealed record ModuleSerialCallbackView(
    int ModuleOrder,
    string ModuleTypeName,
    ModuleKey? ModuleKey,
    double AggregateDurationMs,
    string? SlowestPhase,
    double SlowestPhaseDurationMs,
    int CallbackCount,
    int WorkItemCount);

/// <summary>
/// Represents one system composition phase span and its share of end-to-end composition time.
/// </summary>
public sealed record ModuleSystemPhaseView(
    long Sequence,
    string Name,
    double StartedOffsetMs,
    double DurationMs,
    double EndToEndPercentage);
