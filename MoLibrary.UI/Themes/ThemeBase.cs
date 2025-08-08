using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 主题基类 - 提供默认的代码块主题实现
/// </summary>
public abstract class ThemeBase : IThemeProvider
{
    /// <summary>
    /// 主题名称
    /// </summary>
    public abstract string Name { get; }
    
    /// <summary>
    /// 主题显示名称
    /// </summary>
    public abstract string DisplayName { get; }
    
    /// <summary>
    /// 主题描述
    /// </summary>
    public abstract string Description { get; }
    
    /// <summary>
    /// 创建MudTheme实例
    /// </summary>
    /// <returns>配置好的MudTheme实例</returns>
    public abstract MudTheme CreateTheme();
    
    /// <summary>
    /// 获取明亮模式下的代码块主题
    /// 默认使用Github主题，子类可重写
    /// </summary>
    public virtual CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;
    
    /// <summary>
    /// 获取暗黑模式下的代码块主题
    /// 默认使用GithubDark主题，子类可重写
    /// </summary>
    public virtual CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.GithubDark;
}