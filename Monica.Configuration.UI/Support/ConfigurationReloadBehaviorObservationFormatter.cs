using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;
using MudBlazor;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Formats configuration-consumption evidence for operator-facing UI surfaces.
/// </summary>
internal static class ConfigurationReloadBehaviorObservationFormatter
{
    /// <summary>
    /// Formats an observation kind as a compact localized label.
    /// </summary>
    public static string Label(
        ConfigurationReloadBehaviorObservationKind kind,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return kind switch
        {
            ConfigurationReloadBehaviorObservationKind.Declared =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:Declared:Label"],
            ConfigurationReloadBehaviorObservationKind.Inferred =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:Inferred:Label"],
            ConfigurationReloadBehaviorObservationKind.Unresolved =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:Unresolved:Label"],
            ConfigurationReloadBehaviorObservationKind.NotConsumed =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:NotConsumed:Label"],
            _ => kind.ToString()
        };
    }

    /// <summary>
    /// Formats an observation kind as localized explanatory text.
    /// </summary>
    public static string Description(
        ConfigurationReloadBehaviorObservationKind kind,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return kind switch
        {
            ConfigurationReloadBehaviorObservationKind.Declared =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:Declared:Description"],
            ConfigurationReloadBehaviorObservationKind.Inferred =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:Inferred:Description"],
            ConfigurationReloadBehaviorObservationKind.Unresolved =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:Unresolved:Description"],
            ConfigurationReloadBehaviorObservationKind.NotConsumed =>
                localizer["DefinitionPublication:ReloadContributors:Evidence:NotConsumed:Description"],
            _ => kind.ToString()
        };
    }

    /// <summary>
    /// Gets the semantic UI color for an observation kind.
    /// </summary>
    public static Color Color(ConfigurationReloadBehaviorObservationKind kind)
    {
        return kind switch
        {
            ConfigurationReloadBehaviorObservationKind.Declared => MudBlazor.Color.Primary,
            ConfigurationReloadBehaviorObservationKind.Inferred => MudBlazor.Color.Info,
            ConfigurationReloadBehaviorObservationKind.Unresolved => MudBlazor.Color.Warning,
            ConfigurationReloadBehaviorObservationKind.NotConsumed => MudBlazor.Color.Default,
            _ => MudBlazor.Color.Default
        };
    }
}
