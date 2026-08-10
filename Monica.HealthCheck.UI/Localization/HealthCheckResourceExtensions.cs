using Microsoft.Extensions.Localization;
using Monica.HealthCheck.Models;

namespace Monica.HealthCheck.UI.Localization;

/// <summary>
/// Resolves localized display text for Health Check UI model values.
/// </summary>
public static class HealthCheckResourceExtensions
{
    /// <summary>Gets the localized label for a health state.</summary>
    public static string GetStatusText(
        this IStringLocalizer<HealthCheckResource> localizer,
        HealthCheckState status)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return status switch
        {
            HealthCheckState.Healthy => localizer["Status:Healthy"],
            HealthCheckState.Degraded => localizer["Status:Degraded"],
            HealthCheckState.Unhealthy => localizer["Status:Unhealthy"],
            _ => localizer["Status:Unhealthy"]
        };
    }
}
