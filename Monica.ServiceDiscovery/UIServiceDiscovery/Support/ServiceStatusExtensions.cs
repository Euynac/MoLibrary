using Microsoft.Extensions.Localization;
using Monica.ServiceDiscovery.Localization;
using Monica.ServiceDiscovery.Models;
using MudBlazor;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.Support;

/// <summary>
/// Provides display metadata for <see cref="ServiceStatus"/>.
/// </summary>
public static class ServiceStatusExtensions
{
    private static readonly Dictionary<ServiceStatus, StatusDisplayInfo> Map = new()
    {
        [ServiceStatus.Running] = new(Color.Success, "#4caf50", "Shared:ServiceStatuses:Running", Icons.Material.Filled.CheckCircle),
        [ServiceStatus.Updating] = new(Color.Info, "#2196f3", "Shared:ServiceStatuses:Updating", Icons.Material.Filled.Update),
        [ServiceStatus.Offline] = new(Color.Default, "#9e9e9e", "Shared:ServiceStatuses:Offline", Icons.Material.Filled.CloudOff),
        [ServiceStatus.Error] = new(Color.Error, "#f44336", "Shared:ServiceStatuses:Error", Icons.Material.Filled.Error),
        [ServiceStatus.Unhealthy] = new(Color.Warning, "#ff9800", "Shared:ServiceStatuses:Unhealthy", Icons.Material.Filled.HealthAndSafety),
    };

    /// <summary>
    /// Default display metadata for unmapped status values.
    /// </summary>
    private static readonly StatusDisplayInfo DefaultInfo =
        new(Color.Default, "#757575", "Shared:ServiceStatuses:Unknown", Icons.Material.Filled.Help);

    /// <summary>
    /// Gets the display metadata for the status.
    /// </summary>
    public static StatusDisplayInfo GetDisplayInfo(this ServiceStatus status)
        => Map.TryGetValue(status, out var info) ? info : DefaultInfo;

    /// <summary>
    /// Gets the MudBlazor color.
    /// </summary>
    public static Color GetColor(this ServiceStatus status) => status.GetDisplayInfo().Color;

    /// <summary>
    /// Gets the hex color.
    /// </summary>
    public static string GetHexColor(this ServiceStatus status) => status.GetDisplayInfo().HexColor;

    /// <summary>
    /// Gets the localized display text.
    /// </summary>
    public static string GetText(this ServiceStatus status, IStringLocalizer<ServiceDiscoveryResource> localizer)
        => localizer[status.GetDisplayInfo().TextKey];

    /// <summary>
    /// Gets the icon.
    /// </summary>
    public static string GetIcon(this ServiceStatus status) => status.GetDisplayInfo().Icon;

    /// <summary>
    /// Gets all statuses for UI filters.
    /// </summary>
    public static IEnumerable<ServiceStatus> GetAllStatuses() => Enum.GetValues<ServiceStatus>();
}
