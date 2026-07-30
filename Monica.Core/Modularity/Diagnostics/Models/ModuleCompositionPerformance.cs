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
    /// Host-owned delays before <c>UseMonica()</c> and <c>MapMonica()</c> are excluded. When registration fails before
    /// its completion milestone, this reports the observed terminal duration instead of discarding the partial span.
    /// </summary>
    public double ServiceRegistrationDurationMs => GetServiceRegistrationDurationMs();

    /// <summary>
    /// Gets the exact system-initialization partition between Monica-controlled work and host-owned gaps.
    /// </summary>
    public ModuleCompositionInitializationPerformance Initialization =>
        ModuleCompositionInitializationPerformance.Create(this);

    /// <summary>
    /// Gets the exact service-registration partition between callbacks, blocking waits, and orchestration.
    /// </summary>
    public ModuleServiceRegistrationPerformance ServiceRegistration =>
        ModuleServiceRegistrationPerformance.Create(this);

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

    /// <summary>Gets the first finite, non-negative offset for a lifecycle milestone.</summary>
    internal double? GetMilestoneOffset(ModuleCompositionMilestone milestone)
    {
        var offsetMs = Milestones
            .Where(info => info.Milestone == milestone)
            .OrderBy(static info => info.Sequence)
            .Select(static info => (double?)info.OffsetMs)
            .FirstOrDefault();
        return offsetMs is { } value && double.IsFinite(value)
            ? Math.Max(0, value)
            : null;
    }

    /// <summary>
    /// Resolves completed registration time, or terminal elapsed time when registration ended before its milestone.
    /// </summary>
    internal double GetServiceRegistrationDurationMs()
    {
        var elapsedDurationMs = ModuleCompositionTimingIntervals.NormalizeDuration(ElapsedDurationMs);
        if (GetMilestoneOffset(ModuleCompositionMilestone.ServiceRegistrationCompleted) is not { } completedOffsetMs)
        {
            return elapsedDurationMs;
        }

        return elapsedDurationMs == 0
            ? completedOffsetMs
            : Math.Clamp(completedOffsetMs, 0, elapsedDurationMs);
    }
}
