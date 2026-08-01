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
        var workItemsById = composition.StartupWorkItems.ToDictionary(
            static item => item.WorkItemId,
            StringComparer.Ordinal);
        var milestones = CreateMilestones(composition);
        var barriers = CreateBarriers(composition, workItemsById);
        var workItems = CreateWorkItems(composition.StartupWorkItems);
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
                ModulePerformanceMath.PercentageOf(phase.DurationMs, longestSystemPhaseDurationMs)))
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
                composition.StartupWorkItems.Count),
            CreateCriticalPath(composition),
            milestones,
            barriers,
            workItems,
            serialModules,
            systemPhases);
    }

    /// <summary>
    /// Creates ordered display rows for a module-owned subset of parallel work items.
    /// </summary>
    public static IReadOnlyList<ModuleStartupWorkItemView> CreateWorkItems(
        IEnumerable<ModuleStartupWorkPerformanceInfo> workItems)
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
                milestone.Milestone,
                milestone.OffsetMs,
                Math.Max(0, milestone.OffsetMs - previousOffset),
                index > 0,
                milestone.Milestone is ModuleCompositionMilestone.ApplicationPipelineStarted
                    or ModuleCompositionMilestone.EndpointMappingStarted);
        }

        return result;
    }

    private static IReadOnlyList<ModuleStartupWorkBarrierView> CreateBarriers(
        ModuleCompositionPerformance composition,
        IReadOnlyDictionary<string, ModuleStartupWorkPerformanceInfo> workItemsById)
    {
        return composition.StartupWorkBarriers
            .OrderBy(static barrier => barrier.Sequence)
            .Select(barrier => new ModuleStartupWorkBarrierView(
                barrier.Sequence + 1,
                barrier.Barrier,
                barrier.EnteredOffsetMs,
                barrier.ReleasedOffsetMs,
                barrier.WaitDurationMs,
                barrier.DueWorkItemIds
                    .Select(workItemId => ResolveWorkName(workItemId, workItemsById))
                    .ToArray(),
                barrier.PendingWorkItems
                    .Select(pending => new ModuleStartupWorkBarrierPendingView(
                        ResolveWorkName(pending.WorkItemId, workItemsById),
                        pending.RemainingDurationMs))
                    .ToArray(),
                barrier.ReleasingWorkItemId is null
                    ? null
                    : ResolveWorkName(barrier.ReleasingWorkItemId, workItemsById)))
            .ToArray();
    }

    private static ModuleStartupWorkItemView CreateWorkItem(ModuleStartupWorkPerformanceInfo item)
    {
        return new ModuleStartupWorkItemView(
            item.Sequence,
            item.ModuleRegistrationOrder,
            item.ModuleTypeName,
            item.ModuleKey,
            item.Name,
            item.OriginPhase,
            item.Barrier,
            item.Status,
            item.SubmittedOffsetMs,
            item.QueueDurationMs,
            item.ExecutionDurationMs,
            item.WasPendingAtBarrier,
            item.WasPendingAtBarrier ? item.RemainingAtBarrierMs : null,
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
            slowest?.Phase,
            slowest?.DurationMs ?? 0,
            module.PhaseExecutions.Count,
            module.StartupWorkItems.Count);
    }

    private static ModuleCriticalPathView? CreateCriticalPath(ModuleCompositionPerformance composition)
    {
        var barrier = composition.CriticalBarrier;
        var workItem = composition.CriticalWorkItem;
        if (barrier is null || workItem is null)
        {
            return null;
        }

        return new ModuleCriticalPathView(
            barrier.Barrier,
            workItem.ModuleTypeName,
            workItem.ModuleKey,
            workItem.Name,
            barrier.BlockingWaitDurationMs);
    }

    private static string ResolveWorkName(
        string workItemId,
        IReadOnlyDictionary<string, ModuleStartupWorkPerformanceInfo> workItemsById)
    {
        return workItemsById.TryGetValue(workItemId, out var workItem)
            ? $"{workItem.ModuleTypeName} / {workItem.Name}"
            : workItemId;
    }

}
