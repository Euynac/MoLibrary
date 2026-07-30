namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Partitions service registration into application configuration, module callbacks, blocking waits, and Monica
/// orchestration.
/// </summary>
/// <remarks>
/// The four child durations are mutually exclusive and add up to <see cref="TotalDurationMs"/>. Application
/// configuration takes precedence over blocking waits, which take precedence over callbacks, so malformed or
/// synthetic overlapping spans are never counted twice.
/// </remarks>
public sealed class ModuleServiceRegistrationPerformance
{
    /// <summary>Gets the observed service-registration duration.</summary>
    public double TotalDurationMs { get; init; }

    /// <summary>Gets time spent in the application callback passed to <c>AddMonica(...)</c>.</summary>
    public double ApplicationConfigurationDurationMs { get; init; }

    /// <summary>Gets the union of serial module callback intervals outside blocking checkpoint waits.</summary>
    public double SerialModuleCallbackDurationMs { get; init; }

    /// <summary>Gets the union of startup-blocking checkpoint intervals.</summary>
    public double BlockingWaitDurationMs { get; init; }

    /// <summary>
    /// Gets remaining Monica orchestration time not attributed to application configuration, callbacks, or waits.
    /// </summary>
    public double OrchestrationDurationMs { get; init; }

    internal static ModuleServiceRegistrationPerformance Create(ModuleCompositionPerformance performance)
    {
        var totalDurationMs = performance.GetServiceRegistrationDurationMs();
        if (totalDurationMs == 0)
        {
            return new ModuleServiceRegistrationPerformance();
        }

        var applicationConfigurationIntervals = performance.SystemPhases
            .Where(static phase => string.Equals(
                phase.PhaseName,
                ModuleCompositionSystemPhaseNames.APPLICATION_CONFIGURATION,
                StringComparison.Ordinal))
            .Select(static phase => new ModuleCompositionTimingInterval(
                phase.StartedOffsetMs,
                phase.CompletedOffsetMs))
            .ToArray();
        var blockingIntervals = performance.Checkpoints
            .Where(static checkpoint => checkpoint.PendingWorkItemCount > 0)
            .Select(static checkpoint => new ModuleCompositionTimingInterval(
                checkpoint.EnteredOffsetMs,
                checkpoint.ReleasedOffsetMs))
            .ToArray();
        var callbackIntervals = performance.ModulePhaseExecutions
            .Select(static execution => new ModuleCompositionTimingInterval(
                execution.StartedOffsetMs,
                execution.CompletedOffsetMs))
            .ToArray();
        var applicationConfigurationDurationMs = ModuleCompositionTimingIntervals.MeasureUnion(
            applicationConfigurationIntervals,
            0,
            totalDurationMs);
        var applicationAndBlockingDurationMs = ModuleCompositionTimingIntervals.MeasureUnion(
            applicationConfigurationIntervals.Concat(blockingIntervals),
            0,
            totalDurationMs);
        var blockingWaitDurationMs = Math.Max(
            0,
            applicationAndBlockingDurationMs - applicationConfigurationDurationMs);
        var attributedDurationMs = ModuleCompositionTimingIntervals.MeasureUnion(
            applicationConfigurationIntervals.Concat(blockingIntervals).Concat(callbackIntervals),
            0,
            totalDurationMs);
        var serialModuleCallbackDurationMs = Math.Max(
            0,
            attributedDurationMs - applicationAndBlockingDurationMs);

        return new ModuleServiceRegistrationPerformance
        {
            TotalDurationMs = totalDurationMs,
            ApplicationConfigurationDurationMs = applicationConfigurationDurationMs,
            SerialModuleCallbackDurationMs = serialModuleCallbackDurationMs,
            BlockingWaitDurationMs = blockingWaitDurationMs,
            OrchestrationDurationMs = Math.Max(0, totalDurationMs - attributedDurationMs)
        };
    }
}
