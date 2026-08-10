using Monica.HealthCheck.Models;
using MudBlazor;

namespace Monica.HealthCheck.UI.Support;

/// <summary>
/// Maps health states to the shared MudBlazor semantic presentation contract.
/// </summary>
public static class HealthCheckDisplay
{
    /// <summary>Gets the semantic MudBlazor color for a health state.</summary>
    public static Color Color(HealthCheckState state) => state switch
    {
        HealthCheckState.Healthy => MudBlazor.Color.Success,
        HealthCheckState.Degraded => MudBlazor.Color.Warning,
        HealthCheckState.Unhealthy => MudBlazor.Color.Error,
        _ => MudBlazor.Color.Default
    };

    /// <summary>Gets the semantic MudBlazor alert severity for a health state.</summary>
    public static Severity Severity(HealthCheckState state) => state switch
    {
        HealthCheckState.Healthy => MudBlazor.Severity.Success,
        HealthCheckState.Degraded => MudBlazor.Severity.Warning,
        HealthCheckState.Unhealthy => MudBlazor.Severity.Error,
        _ => MudBlazor.Severity.Normal
    };

    /// <summary>Gets the Material icon for a health state.</summary>
    public static string Icon(HealthCheckState state) => state switch
    {
        HealthCheckState.Healthy => Icons.Material.Filled.CheckCircle,
        HealthCheckState.Degraded => Icons.Material.Filled.Warning,
        HealthCheckState.Unhealthy => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.Help
    };
}
