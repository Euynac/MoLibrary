using MudBlazor;
using Monica.UI.Theming;

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
    /// Current theme identity.
    /// </summary>
    MonicaThemeKind CurrentThemeKind { get; set; }

    /// <summary>
    /// Applies the selected theme and mode as one state transition.
    /// </summary>
    /// <param name="themeKind">Selected theme identity.</param>
    /// <param name="isDarkMode">Whether the theme should use the dark palette.</param>
    void SetTheme(MonicaThemeKind themeKind, bool isDarkMode);

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
