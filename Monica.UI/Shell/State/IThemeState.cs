using MudBlazor;

namespace Monica.UI.Shell.State;

/// <summary>
/// Theme service interface - provides theme management and color conversion functions
/// </summary>
public interface IThemeState
{
    /// <summary>
    /// theme change event
    /// </summary>
    event Action? OnThemeChanged;

    /// <summary>
    /// Whether it is dark mode
    /// </summary>
    bool IsDarkMode { get; set; }

    /// <summary>
    /// Current MudBlazor theme
    /// </summary>
    MudTheme CurrentTheme { get; }

    /// <summary>
    /// Current topic name
    /// </summary>
    string CurrentThemeName { get; set; }

    /// <summary>
    /// Switch theme mode (light and dark switching)
    /// </summary>
    void ToggleTheme();

    /// <summary>
    /// Get the CSS class name of the current theme
    /// </summary>
    string GetThemeCssClass();

    /// <summary>
    /// Get the data-theme attribute value of the theme
    /// </summary>
    string GetThemeDataAttribute();

    /// <summary>
    /// Get the hexadecimal color value corresponding to the current theme according to the MudBlazor Color enumeration
    /// </summary>
    /// <param name="color">MudBlazor color enumeration</param>
    /// <returns>Hex color value (format: #rrggbb)</returns>
    string GetColorHex(Color color);
}
