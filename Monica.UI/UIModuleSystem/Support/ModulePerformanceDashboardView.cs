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
    public double MonicaFrameworkPercentage => PercentageOf(MonicaFrameworkDurationMs, TotalDurationMs);

    /// <summary>
    /// Gets application configuration time as a percentage of total module-system initialization.
    /// </summary>
    public double ApplicationConfigurationPercentage => PercentageOf(
        ApplicationConfigurationDurationMs,
        TotalDurationMs);

    /// <summary>
    /// Gets host-owned gaps as a percentage of total module-system initialization.
    /// </summary>
    public double HostOwnedPercentage => PercentageOf(HostOwnedDurationMs, TotalDurationMs);

    private static double PercentageOf(double durationMs, double totalMs)
        => totalMs <= 0 ? 0 : durationMs / totalMs * 100;
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
    public double ApplicationConfigurationPercentage => PercentageOf(
        ApplicationConfigurationDurationMs,
        TotalDurationMs);

    /// <summary>
    /// Gets serial callback time as a percentage of service registration.
    /// </summary>
    public double SerialModuleCallbackPercentage => PercentageOf(SerialModuleCallbackDurationMs, TotalDurationMs);

    /// <summary>
    /// Gets checkpoint blocking time as a percentage of service registration.
    /// </summary>
    public double BlockingWaitPercentage => PercentageOf(BlockingWaitDurationMs, TotalDurationMs);

    /// <summary>
    /// Gets Monica orchestration time as a percentage of service registration.
    /// </summary>
    public double OrchestrationPercentage => PercentageOf(OrchestrationDurationMs, TotalDurationMs);

    private static double PercentageOf(double durationMs, double totalMs)
        => totalMs <= 0 ? 0 : durationMs / totalMs * 100;
}

/// <summary>
/// Contains parallel worker measurements that overlap the exact elapsed-time partitions.
/// </summary>
public sealed record ModuleParallelActivityView(
    double ActiveSpanMs,
    double AggregateExecutionMs,
    double AggregateQueueMs);

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
/// Represents one system composition phase span relative to the longest observed phase.
/// </summary>
public sealed record ModuleSystemPhaseView(
    long Sequence,
    string Name,
    double StartedOffsetMs,
    double DurationMs,
    double RelativeToLongestPercentage);
