namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes Monica composition as one monotonic timeline.
/// </summary>
/// <remarks>
/// Serial phase, worker execution, and barrier-wait durations are different dimensions and may overlap. The
/// derived aggregates intentionally remain separate and must not be added to infer end-to-end elapsed time.
/// </remarks>
public sealed class ModuleCompositionPerformance
{
    /// <summary>
    /// Gets the UTC timestamp paired with monotonic offset zero.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets the end-to-end composition duration. Once composition completes, this value remains frozen even while
    /// non-blocking startup work continues.
    /// </summary>
    public double ElapsedDurationMs { get; init; }

    /// <summary>
    /// Gets the UTC time when this diagnostic snapshot was observed.
    /// </summary>
    /// <remarks>
    /// Non-blocking startup work can continue after composition completes, so this boundary can be later than the
    /// composition interval represented by <see cref="ElapsedDurationMs"/>.
    /// </remarks>
    public DateTimeOffset ObservedAtUtc { get; init; }

    /// <summary>
    /// Gets the monotonic observation offset from composition origin, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Every offset in this snapshot is bounded by this value. Do not extend or reinterpret
    /// <see cref="ElapsedDurationMs"/> when background startup work completes after composition.
    /// </remarks>
    public double ObservedDurationMs { get; init; }

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
    /// Gets every scheduled startup work item in stable submission order.
    /// </summary>
    public IReadOnlyList<ModuleStartupWorkPerformanceInfo> StartupWorkItems { get; init; } = [];

    /// <summary>
    /// Gets startup-work barrier observations in lifecycle order.
    /// </summary>
    public IReadOnlyList<ModuleStartupWorkBarrierPerformanceInfo> StartupWorkBarriers { get; init; } = [];

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
    /// Gets the monotonic span from the first worker start to the last worker completion or the observation boundary
    /// while work is still running. Queued work does not contribute until it starts.
    /// </summary>
    public double ParallelWorkActiveSpanMs
    {
        get
        {
            var startedWork = StartupWorkItems
                .Where(static work => work.StartedOffsetMs.HasValue)
                .ToArray();
            return startedWork.Length == 0
                ? 0
                : Math.Max(
                    0,
                    startedWork.Max(work => work.CompletedOffsetMs ?? ObservedDurationMs)
                    - startedWork.Min(static work => work.StartedOffsetMs!.Value));
        }
    }

    /// <summary>
    /// Gets aggregate active worker execution time. Concurrent intervals are counted independently.
    /// </summary>
    public double AggregateWorkExecutionDurationMs => StartupWorkItems.Sum(static work => work.ExecutionDurationMs);

    /// <summary>
    /// Gets aggregate time spent waiting for a worker. Concurrent queue intervals are counted independently.
    /// </summary>
    public double AggregateWorkQueueDurationMs => StartupWorkItems.Sum(static work => work.QueueDurationMs);

    /// <summary>
    /// Gets the aggregate serial wait imposed by startup-work barriers.
    /// </summary>
    public double AggregateBarrierWaitDurationMs =>
        StartupWorkBarriers.Sum(static barrier => barrier.BlockingWaitDurationMs);

    /// <summary>
    /// Gets the barrier with the longest blocking wait, or <see langword="null"/> when no barrier blocked.
    /// </summary>
    public ModuleStartupWorkBarrierPerformanceInfo? CriticalBarrier => StartupWorkBarriers
        .Where(static barrier => barrier.BlockingWaitDurationMs > 0)
        .OrderByDescending(static barrier => barrier.BlockingWaitDurationMs)
        .ThenBy(static barrier => barrier.Sequence)
        .FirstOrDefault();

    /// <summary>
    /// Gets the work item that released the longest-waiting barrier, when one was pending.
    /// </summary>
    public ModuleStartupWorkPerformanceInfo? CriticalWorkItem
    {
        get
        {
            var workItemId = CriticalBarrier?.ReleasingWorkItemId;
            return workItemId is null
                ? null
                : StartupWorkItems.FirstOrDefault(work =>
                    string.Equals(work.WorkItemId, workItemId, StringComparison.Ordinal));
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
