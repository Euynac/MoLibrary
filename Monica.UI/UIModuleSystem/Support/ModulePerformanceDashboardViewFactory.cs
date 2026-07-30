using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Projects the Core monotonic composition contract into display-oriented dashboard rows.
/// </summary>
internal static class ModulePerformanceDashboardViewFactory
{
    /// <summary>
    /// Creates one immutable dashboard projection without recomputing legacy timing concepts.
    /// </summary>
    public static ModulePerformanceDashboardView Create(ModuleSystemPerformance performance)
    {
        ArgumentNullException.ThrowIfNull(performance);

        var composition = performance.Composition;
        var workItemsById = composition.WorkItems.ToDictionary(
            static item => item.WorkItemId,
            StringComparer.Ordinal);
        var milestones = CreateMilestones(composition);
        var checkpoints = CreateCheckpoints(composition, workItemsById);
        var workItems = CreateWorkItems(composition.WorkItems);
        var serialModules = performance.Modules
            .Select(CreateSerialModule)
            .OrderByDescending(static module => module.AggregateDurationMs)
            .ThenBy(static module => module.ModuleOrder)
            .ToArray();
        var orderedSystemPhases = composition.SystemPhases
            .OrderBy(static phase => phase.Sequence)
            .ToArray();
        var longestSystemPhaseDurationMs = orderedSystemPhases.Length == 0
            ? 0
            : orderedSystemPhases.Max(static phase => phase.DurationMs);
        var systemPhases = orderedSystemPhases
            .Select(phase => new ModuleSystemPhaseView(
                phase.Sequence,
                phase.PhaseName,
                phase.StartedOffsetMs,
                phase.DurationMs,
                PercentageOf(phase.DurationMs, longestSystemPhaseDurationMs)))
            .ToArray();

        var initialization = composition.Initialization;
        var serviceRegistration = composition.ServiceRegistration;

        return new ModulePerformanceDashboardView(
            new ModulePerformanceSummaryView(
                new ModuleInitializationBreakdownView(
                    initialization.TotalDurationMs,
                    initialization.MonicaFrameworkDurationMs,
                    initialization.ApplicationConfigurationDurationMs,
                    initialization.HostOwnedDurationMs),
                new ModuleServiceRegistrationBreakdownView(
                    serviceRegistration.TotalDurationMs,
                    serviceRegistration.ApplicationConfigurationDurationMs,
                    serviceRegistration.SerialModuleCallbackDurationMs,
                    serviceRegistration.BlockingWaitDurationMs,
                    serviceRegistration.OrchestrationDurationMs),
                new ModuleParallelActivityView(
                    composition.ParallelWorkActiveSpanMs,
                    composition.AggregateWorkExecutionDurationMs,
                    composition.AggregateWorkQueueDurationMs),
                composition.ModulePhaseExecutions.Count(execution =>
                    execution.StartedOffsetMs < serviceRegistration.TotalDurationMs),
                composition.WorkItems.Count),
            CreateCriticalPath(composition),
            milestones,
            checkpoints,
            workItems,
            serialModules,
            systemPhases);
    }

    /// <summary>
    /// Creates ordered display rows for a module-owned subset of parallel work items.
    /// </summary>
    public static IReadOnlyList<ModuleCompositionWorkItemView> CreateWorkItems(
        IEnumerable<ModuleCompositionWorkPerformanceInfo> workItems)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        return workItems
            .Select(CreateWorkItem)
            .OrderByDescending(static item => item.ExecutionDurationMs)
            .ThenBy(static item => item.Sequence)
            .ToArray();
    }

    private static IReadOnlyList<ModuleCompositionMilestoneView> CreateMilestones(
        ModuleCompositionPerformance composition)
    {
        var ordered = composition.Milestones.OrderBy(static milestone => milestone.Sequence).ToArray();
        var result = new ModuleCompositionMilestoneView[ordered.Length];

        for (var index = 0; index < ordered.Length; index++)
        {
            var milestone = ordered[index];
            var previousOffset = index == 0 ? 0 : ordered[index - 1].OffsetMs;
            result[index] = new ModuleCompositionMilestoneView(
                index + 1,
                milestone.Milestone.ToString(),
                milestone.OffsetMs,
                Math.Max(0, milestone.OffsetMs - previousOffset),
                index > 0,
                milestone.Milestone is ModuleCompositionMilestone.ApplicationPipelineStarted
                    or ModuleCompositionMilestone.EndpointMappingStarted);
        }

        return result;
    }

    private static IReadOnlyList<ModuleCompositionCheckpointView> CreateCheckpoints(
        ModuleCompositionPerformance composition,
        IReadOnlyDictionary<string, ModuleCompositionWorkPerformanceInfo> workItemsById)
    {
        return composition.Checkpoints
            .OrderBy(static checkpoint => checkpoint.Sequence)
            .Select(checkpoint => new ModuleCompositionCheckpointView(
                checkpoint.Sequence + 1,
                checkpoint.Deadline.ToString(),
                checkpoint.EnteredOffsetMs,
                checkpoint.ReleasedOffsetMs,
                checkpoint.WaitDurationMs,
                checkpoint.DueWorkItemIds
                    .Select(workItemId => ResolveWorkName(workItemId, workItemsById))
                    .ToArray(),
                checkpoint.PendingWorkItems
                    .Select(pending => new ModuleCompositionCheckpointPendingWorkView(
                        ResolveWorkName(pending.WorkItemId, workItemsById),
                        pending.RemainingDurationMs))
                    .ToArray(),
                checkpoint.ReleasingWorkItemId is null
                    ? null
                    : ResolveWorkName(checkpoint.ReleasingWorkItemId, workItemsById)))
            .ToArray();
    }

    private static ModuleCompositionWorkItemView CreateWorkItem(ModuleCompositionWorkPerformanceInfo item)
    {
        return new ModuleCompositionWorkItemView(
            item.Sequence,
            item.ModuleRegistrationOrder,
            item.ModuleTypeName,
            item.ModuleKey,
            item.Name,
            item.OriginPhase.ToString(),
            item.Deadline.ToString(),
            item.Status.ToString(),
            item.SubmittedOffsetMs,
            item.QueueDurationMs,
            item.ExecutionDurationMs,
            item.WasPendingAtDeadline,
            item.WasPendingAtDeadline ? item.RemainingAtDeadlineMs : null,
            item.ErrorMessage);
    }

    private static ModuleSerialCallbackView CreateSerialModule(ModulePerformanceInfo module)
    {
        var slowest = module.PhaseExecutions
            .GroupBy(static execution => execution.Phase)
            .Select(static executions => new
            {
                Phase = executions.Key,
                DurationMs = executions.Sum(static execution => execution.DurationMs),
                FirstSequence = executions.Min(static execution => execution.Sequence)
            })
            .OrderByDescending(static phase => phase.DurationMs)
            .ThenBy(static phase => phase.FirstSequence)
            .FirstOrDefault();

        return new ModuleSerialCallbackView(
            module.RegistrationOrder,
            module.ModuleTypeName,
            module.IsRuntimeAvailable ? module.ModuleKey : (ModuleKey?)null,
            module.SerialDurationMs,
            slowest?.Phase.ToString(),
            slowest?.DurationMs ?? 0,
            module.PhaseExecutions.Count,
            module.CompositionWorkItems.Count);
    }

    private static ModuleCriticalPathView? CreateCriticalPath(ModuleCompositionPerformance composition)
    {
        var checkpoint = composition.CriticalCheckpoint;
        var workItem = composition.CriticalWorkItem;
        if (checkpoint is null || workItem is null)
        {
            return null;
        }

        return new ModuleCriticalPathView(
            checkpoint.Deadline.ToString(),
            workItem.ModuleTypeName,
            workItem.ModuleKey,
            workItem.Name,
            checkpoint.BlockingWaitDurationMs);
    }

    private static string ResolveWorkName(
        string workItemId,
        IReadOnlyDictionary<string, ModuleCompositionWorkPerformanceInfo> workItemsById)
    {
        return workItemsById.TryGetValue(workItemId, out var workItem)
            ? $"{workItem.ModuleTypeName} / {workItem.Name}"
            : workItemId;
    }

    private static double PercentageOf(double durationMs, double totalMs)
        => totalMs <= 0 ? 0 : durationMs / totalMs * 100;
}
