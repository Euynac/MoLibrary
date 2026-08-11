using Monica.Framework.Seeder.Models;
using MudBlazor;

namespace Monica.Framework.UI.UISeeder.Support;

/// <summary>
/// Maps Seeder diagnostics states onto MudBlazor's shared semantic presentation roles.
/// </summary>
public static class SeederDisplay
{
    /// <summary>Gets the stable DOM identifier for a Seeder's detail trigger.</summary>
    public static string DetailsTriggerId(string seederTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seederTypeName);
        return $"seeder-details-trigger-{Uri.EscapeDataString(seederTypeName)}";
    }

    /// <summary>Gets whether a run state requires one-second polling.</summary>
    public static bool IsActive(SeederRunStatus status) => status is
        SeederRunStatus.Waiting or SeederRunStatus.Running or SeederRunStatus.Aborting;

    /// <summary>Gets the alert severity for an overall run status.</summary>
    public static Severity Severity(SeederRunStatus status) => status switch
    {
        SeederRunStatus.Succeeded => MudBlazor.Severity.Success,
        SeederRunStatus.CompletedWithFailures => MudBlazor.Severity.Warning,
        SeederRunStatus.Aborting => MudBlazor.Severity.Warning,
        SeederRunStatus.Aborted => MudBlazor.Severity.Error,
        SeederRunStatus.Cancelled => MudBlazor.Severity.Warning,
        SeederRunStatus.Waiting => MudBlazor.Severity.Info,
        SeederRunStatus.Running => MudBlazor.Severity.Info,
        _ => MudBlazor.Severity.Normal
    };

    /// <summary>Gets the Material icon for an overall run status.</summary>
    public static string Icon(SeederRunStatus status) => status switch
    {
        SeederRunStatus.Waiting => Icons.Material.Filled.HourglassTop,
        SeederRunStatus.Running => Icons.Material.Filled.Sync,
        SeederRunStatus.Aborting => Icons.Material.Filled.CancelScheduleSend,
        SeederRunStatus.Succeeded => Icons.Material.Filled.TaskAlt,
        SeederRunStatus.CompletedWithFailures => Icons.Material.Filled.Warning,
        SeederRunStatus.Aborted => Icons.Material.Filled.Error,
        SeederRunStatus.Cancelled => Icons.Material.Filled.Cancel,
        _ => Icons.Material.Filled.Help
    };

    /// <summary>Gets the semantic color for a seeder status.</summary>
    public static Color Color(SeederStatus status) => status switch
    {
        SeederStatus.Pending => MudBlazor.Color.Default,
        SeederStatus.Running => MudBlazor.Color.Info,
        SeederStatus.Succeeded => MudBlazor.Color.Success,
        SeederStatus.Failed => MudBlazor.Color.Error,
        SeederStatus.Blocked => MudBlazor.Color.Warning,
        SeederStatus.Cancelled => MudBlazor.Color.Default,
        _ => MudBlazor.Color.Default
    };

    /// <summary>Gets the Material icon for a seeder status.</summary>
    public static string Icon(SeederStatus status) => status switch
    {
        SeederStatus.Pending => Icons.Material.Filled.Schedule,
        SeederStatus.Running => Icons.Material.Filled.PlayCircle,
        SeederStatus.Succeeded => Icons.Material.Filled.CheckCircle,
        SeederStatus.Failed => Icons.Material.Filled.Error,
        SeederStatus.Blocked => Icons.Material.Filled.Block,
        SeederStatus.Cancelled => Icons.Material.Filled.Cancel,
        _ => Icons.Material.Filled.Help
    };

    /// <summary>Gets the semantic color for an attempt status.</summary>
    public static Color Color(SeederAttemptStatus status) => status switch
    {
        SeederAttemptStatus.Running => MudBlazor.Color.Info,
        SeederAttemptStatus.Succeeded => MudBlazor.Color.Success,
        SeederAttemptStatus.Failed => MudBlazor.Color.Error,
        SeederAttemptStatus.Cancelled => MudBlazor.Color.Default,
        _ => MudBlazor.Color.Default
    };

    /// <summary>Gets the semantic color for readiness criticality.</summary>
    public static Color Color(SeederCriticality criticality) => criticality switch
    {
        SeederCriticality.Required => MudBlazor.Color.Primary,
        SeederCriticality.Optional => MudBlazor.Color.Secondary,
        _ => MudBlazor.Color.Default
    };

    /// <summary>Gets the semantic color for failure behavior.</summary>
    public static Color Color(SeederFailureBehavior behavior) => behavior switch
    {
        SeederFailureBehavior.ContinueAndRecord => MudBlazor.Color.Info,
        SeederFailureBehavior.FailFast => MudBlazor.Color.Error,
        _ => MudBlazor.Color.Default
    };

    /// <summary>Gets the semantic color for Seeder readiness.</summary>
    public static Color Color(SeederReadinessStatus status) => status switch
    {
        SeederReadinessStatus.Healthy => MudBlazor.Color.Success,
        SeederReadinessStatus.Degraded => MudBlazor.Color.Warning,
        SeederReadinessStatus.Unhealthy => MudBlazor.Color.Error,
        _ => MudBlazor.Color.Default
    };

    /// <summary>Gets the Material icon for Seeder readiness.</summary>
    public static string Icon(SeederReadinessStatus status) => status switch
    {
        SeederReadinessStatus.Healthy => Icons.Material.Filled.CheckCircle,
        SeederReadinessStatus.Degraded => Icons.Material.Filled.Warning,
        SeederReadinessStatus.Unhealthy => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.Help
    };
}
