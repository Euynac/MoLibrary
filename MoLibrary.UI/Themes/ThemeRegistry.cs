namespace MoLibrary.UI.Themes;

/// <summary>
/// 主题注册表 - 管理所有可用主题
/// </summary>
public static class ThemeRegistry
{
    private static readonly Dictionary<string, IThemeProvider> _themes = new();

    static ThemeRegistry()
    {
        RegisterDefaultThemes();
    }

    /// <summary>
    /// 注册默认主题
    /// </summary>
    private static void RegisterDefaultThemes()
    {
        RegisterTheme(new MudBlazorDefaultTheme());
        RegisterTheme(new MoLibraryDefaultTheme());
        RegisterTheme(new GlassmorphicTheme());
    }

    /// <summary>
    /// 注册主题
    /// </summary>
    /// <param name="themeProvider">主题提供者</param>
    public static void RegisterTheme(IThemeProvider themeProvider)
    {
        _themes[themeProvider.Name] = themeProvider;
    }

    /// <summary>
    /// 获取主题提供者
    /// </summary>
    /// <param name="themeName">主题名称</param>
    /// <returns>主题提供者，如果不存在则返回默认主题</returns>
    public static IThemeProvider GetTheme(string themeName)
    {
        return _themes.TryGetValue(themeName, out var theme) 
            ? theme 
            : _themes["default"]; // 如果主题不存在，返回默认主题
    }

    /// <summary>
    /// 获取所有可用主题
    /// </summary>
    /// <returns>主题信息数组</returns>
    public static (string Name, string DisplayName, string Description)[] GetAvailableThemes()
    {
        return _themes.Values
            .Select(t => (t.Name, t.DisplayName, t.Description))
            .ToArray();
    }

    /// <summary>
    /// 检查主题是否存在
    /// </summary>
    /// <param name="themeName">主题名称</param>
    /// <returns>是否存在</returns>
    public static bool ThemeExists(string themeName)
    {
        return _themes.ContainsKey(themeName);
    }
}