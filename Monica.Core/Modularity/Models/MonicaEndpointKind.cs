namespace Monica.Core.Modularity.Models;

/// <summary>
/// Describes the Monica-owned web endpoint category used by endpoint conventions and diagnostics.
/// </summary>
public enum MonicaEndpointKind
{
    /// <summary>
    /// A Monica Minimal API endpoint.
    /// </summary>
    MinimalApi,

    /// <summary>
    /// A Monica UI endpoint, such as Razor components, redirects, SignalR hubs, or static UI assets.
    /// </summary>
    Ui,

    /// <summary>
    /// A Monica static asset endpoint.
    /// </summary>
    StaticAsset
}
