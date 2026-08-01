using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Presents one immutable, UI-focused projection of module composition diagnostics.
/// </summary>
public sealed record ModulePerformanceDashboardView(
    ModulePerformanceSummaryView Summary,
    ModuleCriticalPathView? CriticalPath,
    IReadOnlyList<ModuleCompositionMilestoneView> Milestones,
    IReadOnlyList<ModuleStartupWorkBarrierView> StartupWorkBarriers,
    IReadOnlyList<ModuleStartupWorkItemView> StartupWorkItems,
    IReadOnlyList<ModuleSerialCallbackView> SerialModules,
    IReadOnlyList<ModuleSystemPhaseView> SystemPhases);

/// <summary>
/// Contains the exact elapsed-time partitions and separately reported parallel activity.
/// </summary>
public sealed record ModulePerformanceSummaryView(
    ModuleInitializationBreakdownView Initialization,
    ModuleServiceRegistrationBreakdownView ServiceRegistration,
    ModuleParallelActivityView ParallelActivity,
    int ServiceRegistrationCallbackCount,
    int WorkItemCount);

/// <summary>
/// Splits module-system initialization into Monica-controlled time and host-owned gaps.
/// </summary>
public sealed record ModuleInitializationBreakdownView(
    double TotalDurationMs,
    double MonicaFrameworkDurationMs,
    double ApplicationConfigurationDurationMs,
    double HostOwnedDurationMs)
{
    /// <summary>
    /// Gets Monica framework time as a percentage of total module-system initialization.
    /// </summary>
    public double MonicaFrameworkPercentage => ModulePerformanceMath.PercentageOf(
        MonicaFrameworkDurationMs,
        TotalDurationMs);

    /// <summary>
    /// Gets application configuration time as a percentage of total module-system initialization.
    /// </summary>
    public double ApplicationConfigurationPercentage => ModulePerformanceMath.PercentageOf(
        ApplicationConfigurationDurationMs,
        TotalDurationMs);

    /// <summary>
    /// Gets host-owned gaps as a percentage of total module-system initialization.
    /// </summary>
    public double HostOwnedPercentage => ModulePerformanceMath.PercentageOf(HostOwnedDurationMs, TotalDurationMs);
}

/// <summary>
/// Splits service registration into its exact, additive timing categories.
/// </summary>
public sealed record ModuleServiceRegistrationBreakdownView(
    double TotalDurationMs,
    double ApplicationConfigurationDurationMs,
    double SerialModuleCallbackDurationMs,
    double BlockingWaitDurationMs,
    double OrchestrationDurationMs)
{
    /// <summary>
    /// Gets application configuration time as a percentage of service registration.
    /// </summary>
    public double ApplicationConfigurationPercentage => ModulePerformanceMath.PercentageOf(
        ApplicationConfigurationDurationMs,
        TotalDurationMs);

    /// <summary>
    /// Gets serial callback time as a percentage of service registration.
    /// </summary>
    public double SerialModuleCallbackPercentage => ModulePerformanceMath.PercentageOf(
        SerialModuleCallbackDurationMs,
        TotalDurationMs);

    /// <summary>
    /// Gets startup-barrier blocking time as a percentage of service registration.
    /// </summary>
    public double BlockingWaitPercentage => ModulePerformanceMath.PercentageOf(
        BlockingWaitDurationMs,
        TotalDurationMs);

    /// <summary>
    /// Gets Monica orchestration time as a percentage of service registration.
    /// </summary>
    public double OrchestrationPercentage => ModulePerformanceMath.PercentageOf(
        OrchestrationDurationMs,
        TotalDurationMs);
}

/// <summary>
/// Contains parallel worker measurements that overlap the exact elapsed-time partitions.
/// </summary>
public sealed record ModuleParallelActivityView(
    double ActiveSpanMs,
    double AggregateExecutionMs,
    double AggregateQueueMs);

/// <summary>
/// Identifies the parallel work item that released the longest-blocking startup barrier.
/// </summary>
public sealed record ModuleCriticalPathView(
    ModuleStartupWorkBarrier Barrier,
    string ReleasingModuleTypeName,
    ModuleKey? ReleasingModuleKey,
    string ReleasingWorkName,
    double WaitDurationMs);

/// <summary>
/// Represents one composition milestone measured from the shared monotonic origin.
/// </summary>
public sealed record ModuleCompositionMilestoneView(
    long Sequence,
    ModuleCompositionMilestone Name,
    double OffsetMs,
    double ElapsedSincePreviousMs,
    bool HasPreviousMilestone,
    bool IsHostOwnedGap);

/// <summary>
/// Represents one startup-work barrier reached by the serial composition pipeline.
/// </summary>
public sealed record ModuleStartupWorkBarrierView(
    long Sequence,
    ModuleStartupWorkBarrier Barrier,
    double EnteredOffsetMs,
    double ReleasedOffsetMs,
    double WaitDurationMs,
    IReadOnlyList<string> DueWorkItemNames,
    IReadOnlyList<ModuleStartupWorkBarrierPendingView> PendingWorkItems,
    string? ReleasingWorkItemName);

/// <summary>
/// Represents work that was still running when a startup barrier was reached.
/// </summary>
public sealed record ModuleStartupWorkBarrierPendingView(
    string WorkItemName,
    double RemainingDurationMs);

/// <summary>
/// Represents one module-owned parallel startup work item.
/// </summary>
public sealed record ModuleStartupWorkItemView(
    long Sequence,
    int ModuleOrder,
    string ModuleTypeName,
    ModuleKey? ModuleKey,
    string Name,
    ModulePhase OriginPhase,
    ModuleStartupWorkBarrier Barrier,
    ModuleStartupWorkStatus Status,
    double SubmittedOffsetMs,
    double QueueDurationMs,
    double ExecutionDurationMs,
    bool WasPendingAtBarrier,
    double? RemainingAtBarrierMs,
    string? ErrorMessage)
{
    /// <summary>
    /// Gets how this work item affects host startup at the time represented by the snapshot.
    /// </summary>
    public ModuleStartupWorkImpact Impact => Barrier switch
    {
        ModuleStartupWorkBarrier.NoBarrier => ModuleStartupWorkImpact.DoesNotBlockStartup,
        _ when Status is ModuleStartupWorkStatus.Queued or ModuleStartupWorkStatus.Running =>
            ModuleStartupWorkImpact.PendingBeforeBarrier,
        _ when WasPendingAtBarrier => ModuleStartupWorkImpact.BlockedBarrier,
        _ => ModuleStartupWorkImpact.CompletedBeforeBarrier
    };
}

/// <summary>
/// Describes a startup work item's current or observed effect on host readiness.
/// </summary>
public enum ModuleStartupWorkImpact
{
    /// <summary>The work is independent from host readiness.</summary>
    DoesNotBlockStartup,

    /// <summary>The required work is live and will be checked when its barrier is reached.</summary>
    PendingBeforeBarrier,

    /// <summary>The work was incomplete at barrier entry and delayed host readiness.</summary>
    BlockedBarrier,

    /// <summary>The work reached a terminal state before barrier entry.</summary>
    CompletedBeforeBarrier
}

/// <summary>
/// Summarizes serial callbacks for one module without mixing in parallel work duration.
/// </summary>
public sealed record ModuleSerialCallbackView(
    int ModuleOrder,
    string ModuleTypeName,
    ModuleKey? ModuleKey,
    double AggregateDurationMs,
    ModulePhase? SlowestPhase,
    double SlowestPhaseDurationMs,
    int CallbackCount,
    int WorkItemCount);

/// <summary>
/// Represents one system composition phase span relative to the longest observed phase.
/// </summary>
public sealed record ModuleSystemPhaseView(
    long Sequence,
    string Name,
    double StartedOffsetMs,
    double DurationMs,
    double RelativeToLongestPercentage);

/// <summary>
/// Provides shared calculations for immutable performance views.
/// </summary>
internal static class ModulePerformanceMath
{
    /// <summary>
    /// Calculates a duration's bounded percentage of a total duration.
    /// </summary>
    public static double PercentageOf(double durationMs, double totalMs)
        => totalMs <= 0 ? 0 : Math.Clamp(durationMs / totalMs * 100, 0, 100);
}
