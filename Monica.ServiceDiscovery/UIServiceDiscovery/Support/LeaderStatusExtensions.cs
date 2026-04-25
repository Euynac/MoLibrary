using Microsoft.Extensions.Localization;
using Monica.ServiceDiscovery.Localization;
using Monica.ServiceDiscovery.Models;
using MudBlazor;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.Support;

/// <summary>
/// Provides display metadata for <see cref="LeaderStatus"/>.
/// </summary>
public static class LeaderStatusExtensions
{
    private static readonly Dictionary<LeaderStatus, StatusDisplayInfo> Map = new()
    {
        [LeaderStatus.Leader] = new(Color.Warning, "Shared:LeaderStatuses:Leader", Icons.Material.Filled.Star),
        [LeaderStatus.Follower] = new(Color.Default, "Shared:LeaderStatuses:Follower", Icons.Material.Filled.Circle),
        [LeaderStatus.Looking] = new(Color.Info, "Shared:LeaderStatuses:Looking", Icons.Material.Filled.Search),
    };

    /// <summary>
    /// Default display metadata for unmapped status values.
    /// </summary>
    private static readonly StatusDisplayInfo DefaultInfo =
        new(Color.Default, "Shared:LeaderStatuses:Unknown", Icons.Material.Filled.Help);

    /// <summary>
    /// Gets the display metadata for the status.
    /// </summary>
    public static StatusDisplayInfo GetDisplayInfo(this LeaderStatus status)
        => Map.TryGetValue(status, out var info) ? info : DefaultInfo;

    /// <summary>
    /// Gets the MudBlazor color.
    /// </summary>
    public static Color GetColor(this LeaderStatus status) => status.GetDisplayInfo().Color;

    /// <summary>
    /// Gets the localized display text.
    /// </summary>
    public static string GetText(this LeaderStatus status, IStringLocalizer<ServiceDiscoveryResource> localizer)
        => localizer[status.GetDisplayInfo().TextKey];

    /// <summary>
    /// Gets the icon.
    /// </summary>
    public static string GetIcon(this LeaderStatus status) => status.GetDisplayInfo().Icon;
}
