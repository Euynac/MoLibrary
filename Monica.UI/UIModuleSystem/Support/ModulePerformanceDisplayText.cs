using Microsoft.Extensions.Localization;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.UI.Localization;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Resolves stable diagnostic identifiers through the module-system localization resource.
/// </summary>
internal static class ModulePerformanceDisplayText
{
    /// <summary>
    /// Gets the localized display name of a composition milestone.
    /// </summary>
    public static string Milestone(
        IStringLocalizer<SharedResource> localizer,
        ModuleCompositionMilestone milestone) => milestone switch
    {
        ModuleCompositionMilestone.CompositionStarted =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:CompositionStarted"],
        ModuleCompositionMilestone.ServiceRegistrationCompleted =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:ServiceRegistrationCompleted"],
        ModuleCompositionMilestone.ApplicationPipelineStarted =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:ApplicationPipelineStarted"],
        ModuleCompositionMilestone.ApplicationPipelineCompleted =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:ApplicationPipelineCompleted"],
        ModuleCompositionMilestone.EndpointMappingStarted =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:EndpointMappingStarted"],
        ModuleCompositionMilestone.CompositionCompleted =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:CompositionCompleted"],
        _ => milestone.ToString()
    };

    /// <summary>
    /// Gets the localized display name of a startup-work barrier.
    /// </summary>
    public static string Barrier(
        IStringLocalizer<SharedResource> localizer,
        ModuleStartupWorkBarrier barrier) => barrier switch
    {
        ModuleStartupWorkBarrier.BeforeTypeDiscovery =>
            localizer["ModuleSystem:StartupWork:Barriers:BeforeTypeDiscovery"],
        ModuleStartupWorkBarrier.BeforePostConfigureServices =>
            localizer["ModuleSystem:StartupWork:Barriers:BeforePostConfigureServices"],
        ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion =>
            localizer["ModuleSystem:StartupWork:Barriers:BeforeServiceRegistrationCompletion"],
        ModuleStartupWorkBarrier.BeforeHostLifecycle =>
            localizer["ModuleSystem:StartupWork:Barriers:BeforeHostLifecycle"],
        ModuleStartupWorkBarrier.NoBarrier =>
            localizer["ModuleSystem:StartupWork:Barriers:NoBarrier"],
        _ => barrier.ToString()
    };

    /// <summary>
    /// Gets the localized display name of a startup-work status.
    /// </summary>
    public static string WorkStatus(
        IStringLocalizer<SharedResource> localizer,
        ModuleStartupWorkStatus status)
    {
        return status switch
        {
            ModuleStartupWorkStatus.Queued => localizer["ModuleSystem:StartupWork:States:Queued"],
            ModuleStartupWorkStatus.Running => localizer["ModuleSystem:StartupWork:States:Running"],
            ModuleStartupWorkStatus.Succeeded => localizer["ModuleSystem:StartupWork:States:Succeeded"],
            ModuleStartupWorkStatus.Failed => localizer["ModuleSystem:StartupWork:States:Failed"],
            _ => status.ToString()
        };
    }

    /// <summary>
    /// Formats one duration with the module-system resource's culture-aware unit.
    /// </summary>
    public static string Duration(IStringLocalizer<SharedResource> localizer, double durationMs)
        => localizer["ModuleSystem:PerformanceMonitoring:Formats:Duration", durationMs];

    /// <summary>
    /// Formats one monotonic offset from module-composition origin.
    /// </summary>
    public static string Offset(IStringLocalizer<SharedResource> localizer, double offsetMs)
        => localizer["ModuleSystem:PerformanceMonitoring:Formats:Offset", offsetMs];

    /// <summary>
    /// Formats one diagnostic percentage.
    /// </summary>
    public static string Percentage(IStringLocalizer<SharedResource> localizer, double percentage)
        => localizer["ModuleSystem:PerformanceMonitoring:Formats:Percentage", percentage];
}
