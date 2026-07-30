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
    public static string Milestone(IStringLocalizer<SharedResource> localizer, string name) => name switch
    {
        nameof(ModuleCompositionMilestone.CompositionStarted) =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:CompositionStarted"],
        nameof(ModuleCompositionMilestone.ServiceRegistrationCompleted) =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:ServiceRegistrationCompleted"],
        nameof(ModuleCompositionMilestone.ApplicationPipelineStarted) =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:ApplicationPipelineStarted"],
        nameof(ModuleCompositionMilestone.ApplicationPipelineCompleted) =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:ApplicationPipelineCompleted"],
        nameof(ModuleCompositionMilestone.EndpointMappingStarted) =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:EndpointMappingStarted"],
        nameof(ModuleCompositionMilestone.CompositionCompleted) =>
            localizer["ModuleSystem:PerformanceMonitoring:Milestones:CompositionCompleted"],
        _ => name
    };

    /// <summary>
    /// Gets the localized display name of a system or module phase.
    /// </summary>
    public static string Phase(IStringLocalizer<SharedResource> localizer, string name) => name switch
    {
        nameof(ModulePhase.None) => localizer["ModuleSystem:CompositionWork:Phases:None"],
        nameof(ModulePhase.ClaimDependencies) => localizer["ModuleSystem:CompositionWork:Phases:ClaimDependencies"],
        nameof(ModulePhase.InitFinalConfigures) => localizer["ModuleSystem:CompositionWork:Phases:InitFinalConfigures"],
        nameof(ModulePhase.ConfigureBuilder) => localizer["ModuleSystem:CompositionWork:Phases:ConfigureBuilder"],
        nameof(ModulePhase.ConfigureServices) => localizer["ModuleSystem:CompositionWork:Phases:ConfigureServices"],
        nameof(ModulePhase.IterateBusinessTypes) => localizer["ModuleSystem:CompositionWork:Phases:IterateBusinessTypes"],
        nameof(ModulePhase.PostConfigureServices) => localizer["ModuleSystem:CompositionWork:Phases:PostConfigureServices"],
        nameof(ModulePhase.ConfigureApplicationBuilder) => localizer["ModuleSystem:CompositionWork:Phases:ConfigureApplicationBuilder"],
        nameof(ModulePhase.ConfigureEndpoints) => localizer["ModuleSystem:CompositionWork:Phases:ConfigureEndpoints"],
        nameof(ModulePhase.Disabled) => localizer["ModuleSystem:CompositionWork:Phases:Disabled"],
        "ConfigureBuilderAndServices" =>
            localizer["ModuleSystem:CompositionWork:Phases:ConfigureBuilderAndServices"],
        _ => name
    };

    /// <summary>
    /// Gets the localized display name of a parallel-work deadline.
    /// </summary>
    public static string Deadline(IStringLocalizer<SharedResource> localizer, string name) => name switch
    {
        nameof(ModuleCompositionWorkDeadline.BeforeBusinessTypeIteration) =>
            localizer["ModuleSystem:CompositionWork:Deadlines:BeforeBusinessTypeIteration"],
        nameof(ModuleCompositionWorkDeadline.BeforePostConfigureServices) =>
            localizer["ModuleSystem:CompositionWork:Deadlines:BeforePostConfigureServices"],
        nameof(ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion) =>
            localizer["ModuleSystem:CompositionWork:Deadlines:BeforeServiceRegistrationCompletion"],
        _ => name
    };

    /// <summary>
    /// Gets the localized display name of a parallel-work status.
    /// </summary>
    public static string WorkStatus(IStringLocalizer<SharedResource> localizer, string name)
    {
        return name switch
        {
            nameof(ModuleCompositionWorkStatus.Succeeded) => localizer["ModuleSystem:CompositionWork:States:Succeeded"],
            nameof(ModuleCompositionWorkStatus.Failed) => localizer["ModuleSystem:CompositionWork:States:Failed"],
            _ => name
        };
    }
}
