using Monica.UI.Theming.Definitions;

namespace Monica.UI.Theming;

/// <summary>
/// Theme Registry - Manage all available themes
/// </summary>
public static class ThemeCatalog
{
    private static readonly Dictionary<MonicaThemeKind, IThemeDefinition> _themes = new();

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
        RegisterTheme(new FluentDesignTheme());
        RegisterTheme(new FreshTheme());
        RegisterTheme(new MacaronSweetheartTheme());
        RegisterTheme(new InkLandscapeTheme());
        RegisterTheme(new ZenInkTheme());
    }

    /// <summary>
    /// Register theme.
    /// </summary>
    /// <param name="themeProvider">Theme provider.</param>
    public static void RegisterTheme(IThemeDefinition themeProvider)
    {
        _themes[themeProvider.Kind] = themeProvider;
    }

    /// <summary>
    /// Gets a theme provider.
    /// </summary>
    /// <param name="themeKind">Theme identity.</param>
    /// <returns>The requested theme provider, or the default theme provider when the requested theme is not registered.</returns>
    public static IThemeDefinition GetTheme(MonicaThemeKind themeKind)
    {
        return _themes.TryGetValue(themeKind, out var theme) 
            ? theme 
            : _themes[MonicaThemeKind.Default];
    }

    /// <summary>
    /// Gets all available themes.
    /// </summary>
    /// <returns>Theme information array.</returns>
    public static (MonicaThemeKind Kind, string DisplayName, string Description)[] GetAvailableThemes()
    {
        return _themes.Values
            .Select(t => (t.Kind, t.DisplayName, t.Description))
            .ToArray();
    }

    /// <summary>
    /// Checks whether the theme exists.
    /// </summary>
    /// <param name="themeKind">Theme identity.</param>
    /// <returns><see langword="true"/> when the theme is registered.</returns>
    public static bool ThemeExists(MonicaThemeKind themeKind)
    {
        return _themes.ContainsKey(themeKind);
    }
}
