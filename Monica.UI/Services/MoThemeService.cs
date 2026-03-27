using MudBlazor;
using Monica.UI.Themes;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.UI.Services;

/// <summary>
/// Monica Theme Service - Manage theme switching and custom styles
/// </summary>
public class MoThemeService(IOptions<ModuleUICoreOption> options) : IMoThemeService
{
    private bool _isDarkMode = false;
    private MudTheme _currentTheme = ThemeRegistry.GetTheme("default").CreateTheme();
    private string _currentThemeName = "default";
    private readonly ModuleUICoreOption _options = options.Value;

    public event Action? OnThemeChanged;

    public bool IsDarkMode 
    { 
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                OnThemeChanged?.Invoke();
            }
        }
    }

    public MudTheme CurrentTheme => _currentTheme;
    
    public string CurrentThemeName
    {
        get => _currentThemeName;
        set
        {
            if (_currentThemeName != value)
            {
                _currentThemeName = value;
                _currentTheme = CreateThemeByName(value);
                OnThemeChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// List of available themes
    /// </summary>
    public static (string Name, string DisplayName, string Description)[] AvailableThemes 
        => ThemeRegistry.GetAvailableThemes();

    /// <summary>
    /// Create a topic based on topic name
    /// </summary>
    private MudTheme CreateThemeByName(string themeName)
    {
        return ThemeRegistry.GetTheme(themeName).CreateTheme();
    }


    /// <summary>
    /// Switch theme mode
    /// </summary>
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    /// <summary>
    /// Get the CSS class name of the current theme
    /// </summary>
    public string GetThemeCssClass()
    {
        var mode = IsDarkMode ? "dark" : "light";
        return $"mo-theme-{_currentThemeName}-{mode}";
    }
    
    /// <summary>
    /// Get the data-theme attribute value of the theme
    /// </summary>
    public string GetThemeDataAttribute()
    {
        var mode = IsDarkMode ? "dark" : "light";
        return $"{_currentThemeName}-{mode}";
    }

    /// <summary>
    /// Get the hexadecimal color value corresponding to the current theme according to the MudBlazor Color enumeration
    /// </summary>
    /// <param name="color">MudBlazor color enumeration</param>
    /// <returns>Hex color value (format: #rrggbb)</returns>
    public string GetColorHex(Color color)
    {
        Palette palette = IsDarkMode ? CurrentTheme.PaletteDark : CurrentTheme.PaletteLight;

        // Color.Default and Color.Inherit use GrayDefault which is a string
        if (color == Color.Default || color == Color.Inherit)
        {
            return palette.GrayDefault;
        }

        var mudColor = color switch
        {
            Color.Primary => palette.Primary,
            Color.Secondary => palette.Secondary,
            Color.Tertiary => palette.Tertiary,
            Color.Info => palette.Info,
            Color.Success => palette.Success,
            Color.Warning => palette.Warning,
            Color.Error => palette.Error,
            Color.Dark => palette.Dark,
            Color.Surface => palette.Surface,
            _ => palette.Primary
        };

        // MudColor.Value returns #rrggbbaa (8 chars), truncate to #rrggbb (7 chars) for chart compatibility
        return mudColor.Value[..7];
    }
}