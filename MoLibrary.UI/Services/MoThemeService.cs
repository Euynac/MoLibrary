using MudBlazor;
using MoLibrary.UI.Themes;

namespace MoLibrary.UI.Services;

/// <summary>
/// MoLibrary主题服务 - 管理主题切换和自定义样式
/// </summary>
public class MoThemeService
{
    private bool _isDarkMode = false;
    private MudTheme _currentTheme;
    private string _currentThemeName = "default";

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

    public MoThemeService()
    {
        _currentTheme = ThemeRegistry.GetTheme("default").CreateTheme();
    }
    
    /// <summary>
    /// 可用的主题列表
    /// </summary>
    public static (string Name, string DisplayName, string Description)[] AvailableThemes 
        => ThemeRegistry.GetAvailableThemes();

    /// <summary>
    /// 根据主题名称创建主题
    /// </summary>
    private MudTheme CreateThemeByName(string themeName)
    {
        return ThemeRegistry.GetTheme(themeName).CreateTheme();
    }


    /// <summary>
    /// 切换主题模式
    /// </summary>
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    /// <summary>
    /// 获取当前主题的CSS类名
    /// </summary>
    public string GetThemeCssClass()
    {
        var mode = IsDarkMode ? "dark" : "light";
        return $"mo-theme-{_currentThemeName}-{mode}";
    }
    
    /// <summary>
    /// 获取主题的data-theme属性值
    /// </summary>
    public string GetThemeDataAttribute()
    {
        var mode = IsDarkMode ? "dark" : "light";
        return $"{_currentThemeName}-{mode}";
    }
}