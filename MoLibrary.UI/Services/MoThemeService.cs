using MudBlazor;
using MoLibrary.UI.Themes;
using Microsoft.Extensions.Options;
using MoLibrary.UI.Modules;

namespace MoLibrary.UI.Services;

/// <summary>
/// MoLibrary主题服务 - 管理主题切换和自定义样式
/// </summary>
public class MoThemeService(IOptions<ModuleUICoreOption> options) : IMoThemeService
{
    private bool _isDarkMode = false;
    private MudTheme _currentTheme = new();
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
    /// 初始化主题服务
    /// </summary>
    public void Initialize()
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

    /// <summary>
    /// 根据 MudBlazor Color 枚举获取当前主题对应的十六进制颜色值
    /// </summary>
    /// <param name="color">MudBlazor 颜色枚举</param>
    /// <returns>十六进制颜色值（格式：#rrggbb）</returns>
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