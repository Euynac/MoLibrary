using Monica.UI.Theming.Definitions;

namespace Monica.UI.Theming;

/// <summary>
/// Theme Registry - Manage all available themes
/// </summary>
public static class ThemeCatalog
{
    private static readonly Dictionary<string, IThemeDefinition> _themes = new();

    static ThemeCatalog()
    {
        RegisterDefaultThemes();
    }

    /// <summary>
    /// Register default theme
    /// </summary>
    private static void RegisterDefaultThemes()
    {
        RegisterTheme(new MudBlazorDefaultTheme());
        RegisterTheme(new DefaultTheme());
        RegisterTheme(new HermesTealTheme());
        RegisterTheme(new VibeUsageMatrixTheme());
        RegisterTheme(new MaterialDesign3Theme());
        RegisterTheme(new FreshTheme());
        RegisterTheme(new InkLandscapeTheme());
        RegisterTheme(new ZenInkTheme());
    }

    /// <summary>
    /// Register theme
    /// </summary>
    /// <param name="themeProvider">Theme Provider</param>
    public static void RegisterTheme(IThemeDefinition themeProvider)
    {
        _themes[themeProvider.Name] = themeProvider;
    }

    /// <summary>
    /// Get theme provider
    /// </summary>
    /// <param name="themeName">Theme name</param>
    /// <returns>Theme provider; returns the default theme when the requested one does not exist.</returns>
    public static IThemeDefinition GetTheme(string themeName)
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
