using MudBlazor;

namespace Monica.UI.Services;

/// <summary>
/// 主题服务接口 - 提供主题管理和颜色转换功能
/// </summary>
public interface IMoThemeService
{
    /// <summary>
    /// 主题变更事件
    /// </summary>
    event Action? OnThemeChanged;

    /// <summary>
    /// 是否为暗黑模式
    /// </summary>
    bool IsDarkMode { get; set; }

    /// <summary>
    /// 当前 MudBlazor 主题
    /// </summary>
    MudTheme CurrentTheme { get; }

    /// <summary>
    /// 当前主题名称
    /// </summary>
    string CurrentThemeName { get; set; }

    /// <summary>
    /// 切换主题模式（明暗切换）
    /// </summary>
    void ToggleTheme();

    /// <summary>
    /// 获取当前主题的 CSS 类名
    /// </summary>
    string GetThemeCssClass();

    /// <summary>
    /// 获取主题的 data-theme 属性值
    /// </summary>
    string GetThemeDataAttribute();

    /// <summary>
    /// 根据 MudBlazor Color 枚举获取当前主题对应的十六进制颜色值
    /// </summary>
    /// <param name="color">MudBlazor 颜色枚举</param>
    /// <returns>十六进制颜色值（格式：#rrggbb）</returns>
    string GetColorHex(Color color);
}
