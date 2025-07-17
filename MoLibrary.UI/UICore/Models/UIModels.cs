using Microsoft.AspNetCore.Components.Routing;

namespace MoLibrary.UI.UICore.Models;

/// <summary>
/// UI页面信息
/// </summary>
public class UIPageInfo
{
    /// <summary>
    /// 路由路径
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// 组件类型
    /// </summary>
    public required Type ComponentType { get; init; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 图标
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 分类
    /// </summary>
    public string? Category { get; init; }
}

/// <summary>
/// 导航菜单项
/// </summary>
public class UINavItem
{
    /// <summary>
    /// 显示文本
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 链接地址
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// 图标
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 是否展开（对于有子项的菜单）
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// 导航项分类
    /// </summary>
    public string? Category { get; set; }
    /// <summary>
    /// 子菜单项
    /// </summary>
    public List<UINavItem> Children { get; init; } = new();

    /// <summary>
    /// 点击事件
    /// </summary>
    public Action? OnClick { get; init; }

    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// 导航链接匹配模式
    /// </summary>
    public NavLinkMatch NavLinkMatch { get; init; } = NavLinkMatch.Prefix;
} 