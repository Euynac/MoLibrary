namespace Monica.UI.Themes;

/// <summary>
/// Theme Registry - Manage all available themes
/// </summary>
public static class ThemeRegistry
{
    private static readonly Dictionary<string, IThemeProvider> _themes = new();

    static ThemeRegistry()
    {
        RegisterDefaultThemes();
    }

    /// <summary>
    /// Register default theme
    /// </summary>
    private static void RegisterDefaultThemes()
    {
        RegisterTheme(new ThemeMudBlazorDefault());
        RegisterTheme(new ThemeMonicaDefault());
        RegisterTheme(new ThemeGlassmorphic());
        RegisterTheme(new ThemeNeonPulse());
        RegisterTheme(new ThemeFresh());
        
        // Register new themes.
        RegisterTheme(new ThemeAuroraFlow());
        RegisterTheme(new ThemeInkLandscape());
        RegisterTheme(new ThemeMacaronSweet());
        RegisterTheme(new ThemeDeepOcean());
        RegisterTheme(new ThemeVintagePress());
    }

    /// <summary>
    /// Register theme
    /// </summary>
    /// <param name="themeProvider">Theme Provider</param>
    public static void RegisterTheme(IThemeProvider themeProvider)
    {
        _themes[themeProvider.Name] = themeProvider;
    }

    /// <summary>
    /// Get theme provider
    /// </summary>
    /// <param name="themeName">Theme name</param>
    /// <returns>Theme provider; returns the default theme when the requested one does not exist.</returns>
    public static IThemeProvider GetTheme(string themeName)
    {
        return _themes.TryGetValue(themeName, out var theme) 
            ? theme 
            : _themes["default"]; // Return the default theme when the requested theme does not exist.
    }

    /// <summary>
    /// Get all available themes
    /// </summary>
    /// <returns>Theme information array</returns>
    public static (string Name, string DisplayName, string Description)[] GetAvailableThemes()
    {
        return _themes.Values
            .Select(t => (t.Name, t.DisplayName, t.Description))
            .ToArray();
    }

    /// <summary>
    /// Check whether the theme exists
    /// </summary>
    /// <param name="themeName">Theme name</param>
    /// <returns>Exists</returns>
    public static bool ThemeExists(string themeName)
    {
        return _themes.ContainsKey(themeName);
    }
}
