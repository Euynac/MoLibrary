using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Formats configuration reload behavior metadata for operator-facing UI surfaces.
/// </summary>
internal static class ConfigurationReloadBehaviorFormatter
{
    /// <summary>
    /// Formats a reload behavior as a compact display label.
    /// </summary>
    /// <param name="behavior">The reload behavior to display.</param>
    /// <param name="localizer">The localizer used for user-facing copy.</param>
    /// <returns>A localized label suitable for chips, tables, and tooltips.</returns>
    public static string Label(
        ConfigurationReloadBehavior behavior,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return behavior switch
        {
            ConfigurationReloadBehavior.Inherit => localizer["ReloadBehaviors:Labels:Inherit"],
            ConfigurationReloadBehavior.Unknown => localizer["ReloadBehaviors:Labels:Unknown"],
            ConfigurationReloadBehavior.OnlineReloadable => localizer["ReloadBehaviors:Labels:OnlineReloadable"],
            ConfigurationReloadBehavior.RequiresRestart => localizer["ReloadBehaviors:Labels:RequiresRestart"],
            ConfigurationReloadBehavior.StaticAfterStartup => localizer["ReloadBehaviors:Labels:StaticAfterStartup"],
            _ => localizer["ReloadBehaviors:Labels:Unknown"]
        };
    }

    /// <summary>
    /// Formats a reload behavior as explanatory helper text.
    /// </summary>
    /// <param name="behavior">The reload behavior to describe.</param>
    /// <param name="localizer">The localizer used for user-facing copy.</param>
    /// <returns>A localized explanation of the operational impact.</returns>
    public static string Description(
        ConfigurationReloadBehavior behavior,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return behavior switch
        {
            ConfigurationReloadBehavior.Inherit => localizer["ReloadBehaviors:Descriptions:Inherit"],
            ConfigurationReloadBehavior.Unknown => localizer["ReloadBehaviors:Descriptions:Unknown"],
            ConfigurationReloadBehavior.OnlineReloadable => localizer["ReloadBehaviors:Descriptions:OnlineReloadable"],
            ConfigurationReloadBehavior.RequiresRestart => localizer["ReloadBehaviors:Descriptions:RequiresRestart"],
            ConfigurationReloadBehavior.StaticAfterStartup => localizer["ReloadBehaviors:Descriptions:StaticAfterStartup"],
            _ => localizer["ReloadBehaviors:Descriptions:Unknown"]
        };
    }
}
