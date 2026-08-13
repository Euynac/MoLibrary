using Microsoft.Extensions.Localization;
using Monica.OpenTelemetry.InProcessCollector.Facades;
using Monica.OpenTelemetry.UI.Localization;

namespace Monica.OpenTelemetry.UI.UIOpenTelemetry.State;

/// <summary>
/// Creates component-owned OpenTelemetry dashboard sessions from circuit-scoped dependencies.
/// </summary>
public sealed class OpenTelemetryDashboardPageStateFactory(
    OpenTelemetryFacade facade,
    IStringLocalizer<OpenTelemetryUIResource> localizer)
{
    /// <summary>
    /// Creates a fresh dashboard state whose async resources are owned by one page instance.
    /// </summary>
    public OpenTelemetryDashboardPageState Create()
    {
        return new OpenTelemetryDashboardPageState(facade, localizer["Status:LoadFailed"]);
    }
}
