namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Partitions module-system initialization into Monica framework work, application configuration, and host-owned gaps.
/// </summary>
/// <remarks>
/// The three child durations are mutually exclusive and add up to <see cref="TotalDurationMs"/>. Host-owned gaps
/// include time between completed service registration, <c>UseMonica()</c>, and <c>MapMonica()</c>. Application
/// configuration is the caller-owned callback passed to <c>AddMonica(...)</c>.
/// </remarks>
internal sealed class ModuleCompositionInitializationPerformance
{
    /// <summary>Gets the complete observed module-system initialization duration.</summary>
    public double TotalDurationMs { get; init; }

    /// <summary>Gets elapsed time spent in Monica framework composition work.</summary>
    public double MonicaFrameworkDurationMs { get; init; }

    /// <summary>Gets elapsed time spent in the application's <c>AddMonica(...)</c> configuration callback.</summary>
    public double ApplicationConfigurationDurationMs { get; init; }

    /// <summary>Gets elapsed time outside Monica-owned composition boundaries.</summary>
    public double HostOwnedDurationMs { get; init; }

    internal static ModuleCompositionInitializationPerformance Create(ModuleCompositionPerformance performance)
    {
        var totalDurationMs = ResolveTotalDuration(performance);
        if (totalDurationMs == 0)
        {
            return new ModuleCompositionInitializationPerformance();
        }

        var intervals = new List<ModuleCompositionTimingInterval>();
        var serviceRegistrationDurationMs = performance.GetServiceRegistrationDurationMs();
        if (serviceRegistrationDurationMs > 0)
        {
            intervals.Add(new ModuleCompositionTimingInterval(0, serviceRegistrationDurationMs));
        }

        var isCompletedGenericComposition = performance.GetMilestoneOffset(
                ModuleCompositionMilestone.CompositionCompleted) is not null
            && performance.GetMilestoneOffset(ModuleCompositionMilestone.ApplicationPipelineStarted) is null
            && performance.GetMilestoneOffset(ModuleCompositionMilestone.EndpointMappingStarted) is null;
        if (isCompletedGenericComposition)
        {
            intervals.Add(new ModuleCompositionTimingInterval(
                serviceRegistrationDurationMs,
                totalDurationMs));
        }

        AddLifecycleInterval(
            intervals,
            performance,
            ModuleCompositionMilestone.ApplicationPipelineStarted,
            ModuleCompositionMilestone.ApplicationPipelineCompleted,
            totalDurationMs);
        AddLifecycleInterval(
            intervals,
            performance,
            ModuleCompositionMilestone.EndpointMappingStarted,
            ModuleCompositionMilestone.CompositionCompleted,
            totalDurationMs);

        var broadMonicaBoundaryDurationMs = ModuleCompositionTimingIntervals.MeasureUnion(
            intervals,
            0,
            totalDurationMs);
        var hostOwnedDurationMs = Math.Max(0, totalDurationMs - broadMonicaBoundaryDurationMs);
        var applicationConfigurationDurationMs = performance.ServiceRegistration
            .ApplicationConfigurationDurationMs;
        return new ModuleCompositionInitializationPerformance
        {
            TotalDurationMs = totalDurationMs,
            MonicaFrameworkDurationMs = Math.Max(
                0,
                broadMonicaBoundaryDurationMs - applicationConfigurationDurationMs),
            ApplicationConfigurationDurationMs = applicationConfigurationDurationMs,
            HostOwnedDurationMs = hostOwnedDurationMs
        };
    }

    private static double ResolveTotalDuration(ModuleCompositionPerformance performance)
    {
        var elapsedDurationMs = ModuleCompositionTimingIntervals.NormalizeDuration(performance.ElapsedDurationMs);
        if (performance.GetMilestoneOffset(ModuleCompositionMilestone.CompositionCompleted) is not { } completedOffsetMs)
        {
            return elapsedDurationMs;
        }

        return elapsedDurationMs == 0
            ? completedOffsetMs
            : Math.Min(elapsedDurationMs, completedOffsetMs);
    }

    private static void AddLifecycleInterval(
        ICollection<ModuleCompositionTimingInterval> intervals,
        ModuleCompositionPerformance performance,
        ModuleCompositionMilestone startedMilestone,
        ModuleCompositionMilestone completedMilestone,
        double totalDurationMs)
    {
        if (performance.GetMilestoneOffset(startedMilestone) is not { } startedOffsetMs)
        {
            return;
        }

        var completedOffsetMs = performance.GetMilestoneOffset(completedMilestone) ?? totalDurationMs;
        intervals.Add(new ModuleCompositionTimingInterval(startedOffsetMs, completedOffsetMs));
    }
}
