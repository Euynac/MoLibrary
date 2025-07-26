using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 主题提供者接口
/// </summary>
public interface IThemeProvider
{
    /// <summary>
    /// 主题名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 主题显示名称
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// 主题描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 创建MudTheme实例
    /// </summary>
    /// <returns>配置好的MudTheme实例</returns>
    MudTheme CreateTheme();
}