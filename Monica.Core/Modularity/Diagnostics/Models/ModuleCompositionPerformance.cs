namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes Monica composition as one monotonic timeline.
/// </summary>
/// <remarks>
/// Serial phase, worker execution, and checkpoint-wait durations are different dimensions and may overlap. The
/// derived aggregates intentionally remain separate and must not be added to infer end-to-end elapsed time.
/// </remarks>
public sealed class ModuleCompositionPerformance
{
    /// <summary>
    /// Gets the UTC timestamp paired with monotonic offset zero.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets the end-to-end elapsed duration observed when the snapshot was captured.
    /// </summary>
    public double ElapsedDurationMs { get; init; }

    /// <summary>
    /// Gets the monotonic elapsed duration from <c>AddMonica(...)</c> entry through completed service registration.
    /// Host-owned delays before <c>UseMonica()</c> and <c>MapMonica()</c> are excluded.
    /// </summary>
    public double ServiceRegistrationDurationMs => Milestones
        .FirstOrDefault(static milestone =>
            milestone.Milestone == ModuleCompositionMilestone.ServiceRegistrationCompleted)
        ?.OffsetMs ?? 0;

    /// <summary>
    /// Gets lifecycle milestones in occurrence order.
    /// </summary>
    public IReadOnlyList<ModuleCompositionMilestonePerformanceInfo> Milestones { get; init; } = [];

    /// <summary>
    /// Gets system-level serial phase executions in occurrence order.
    /// </summary>
    public IReadOnlyList<ModuleSystemPhasePerformanceInfo> SystemPhases { get; init; } = [];

    /// <summary>
    /// Gets every module callback execution in occurrence order.
    /// </summary>
    public IReadOnlyList<ModulePhaseExecutionPerformanceInfo> ModulePhaseExecutions { get; init; } = [];

    /// <summary>
    /// Gets every scheduled composition work item in stable submission order.
    /// </summary>
    public IReadOnlyList<ModuleCompositionWorkPerformanceInfo> WorkItems { get; init; } = [];

    /// <summary>
    /// Gets checkpoint observations in lifecycle order.
    /// </summary>
    public IReadOnlyList<ModuleCompositionCheckpointPerformanceInfo> Checkpoints { get; init; } = [];

    /// <summary>
    /// Gets the aggregate duration of all system-level serial phases.
    /// </summary>
    public double AggregateSystemPhaseDurationMs => SystemPhases.Sum(static phase => phase.DurationMs);

    /// <summary>
    /// Gets the aggregate duration of all serial module callbacks.
    /// </summary>
    public double AggregateSerialModuleDurationMs =>
        ModulePhaseExecutions.Sum(static execution => execution.DurationMs);

    /// <summary>
    /// Gets the monotonic span from the first worker start to the last worker completion.
    /// </summary>
    public double ParallelWorkActiveSpanMs => WorkItems.Count == 0
        ? 0
        : Math.Max(
            0,
            WorkItems.Max(static work => work.CompletedOffsetMs)
            - WorkItems.Min(static work => work.StartedOffsetMs));

    /// <summary>
    /// Gets aggregate active worker execution time. Concurrent intervals are counted independently.
    /// </summary>
    public double AggregateWorkExecutionDurationMs => WorkItems.Sum(static work => work.ExecutionDurationMs);

    /// <summary>
    /// Gets aggregate time spent waiting for a worker. Concurrent queue intervals are counted independently.
    /// </summary>
    public double AggregateWorkQueueDurationMs => WorkItems.Sum(static work => work.QueueDurationMs);

    /// <summary>
    /// Gets the aggregate serial wait imposed by composition checkpoints.
    /// </summary>
    public double AggregateCheckpointWaitDurationMs =>
        Checkpoints.Sum(static checkpoint => checkpoint.BlockingWaitDurationMs);

    /// <summary>
    /// Gets the checkpoint with the longest blocking wait, or <see langword="null"/> when no checkpoint blocked.
    /// </summary>
    public ModuleCompositionCheckpointPerformanceInfo? CriticalCheckpoint => Checkpoints
        .Where(static checkpoint => checkpoint.BlockingWaitDurationMs > 0)
        .OrderByDescending(static checkpoint => checkpoint.BlockingWaitDurationMs)
        .ThenBy(static checkpoint => checkpoint.Sequence)
        .FirstOrDefault();

    /// <summary>
    /// Gets the work item that released the longest-waiting checkpoint, when one was pending.
    /// </summary>
    public ModuleCompositionWorkPerformanceInfo? CriticalWorkItem
    {
        get
        {
            var workItemId = CriticalCheckpoint?.ReleasingWorkItemId;
            return workItemId is null
                ? null
                : WorkItems.FirstOrDefault(work => string.Equals(work.WorkItemId, workItemId, StringComparison.Ordinal));
        }
    }
}
