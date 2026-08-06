namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Identifies a host-owned milestone on the Monica composition timeline.
/// </summary>
internal enum ModuleCompositionMilestone
{
    /// <summary><c>AddMonica(...)</c> started composition profiling.</summary>
    CompositionStarted,

    /// <summary><c>AddMonica(...)</c> completed service registration.</summary>
    ServiceRegistrationCompleted,

    /// <summary><c>UseMonica()</c> began configuring the application pipeline.</summary>
    ApplicationPipelineStarted,

    /// <summary><c>UseMonica()</c> completed configuring the application pipeline.</summary>
    ApplicationPipelineCompleted,

    /// <summary><c>MapMonica()</c> began endpoint mapping.</summary>
    EndpointMappingStarted,

    /// <summary>Monica composition completed at the host-appropriate boundary.</summary>
    CompositionCompleted
}

/// <summary>
/// Describes one lifecycle milestone relative to composition start.
/// </summary>
internal sealed class ModuleCompositionMilestonePerformanceInfo
{
    /// <summary>Gets the milestone.</summary>
    public ModuleCompositionMilestone Milestone { get; init; }

    /// <summary>Gets its zero-based occurrence sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the UTC timestamp observed at the milestone.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Gets the monotonic offset from composition start, in milliseconds.</summary>
    public double OffsetMs { get; init; }
}
