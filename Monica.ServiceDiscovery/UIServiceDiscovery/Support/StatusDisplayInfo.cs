using MudBlazor;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.Support;

/// <summary>
/// Represents the shared display metadata for a status value.
/// </summary>
/// <param name="Color">The MudBlazor color.</param>
/// <param name="HexColor">The hex color used by CSS-based visualizations.</param>
/// <param name="TextKey">The localization key for the status text.</param>
/// <param name="Icon">The MudBlazor icon.</param>
public record StatusDisplayInfo(
    Color Color,
    string HexColor,
    string TextKey,
    string Icon
);
