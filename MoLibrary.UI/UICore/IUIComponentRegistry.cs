using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace MoLibrary.UI.UICore;

/// <summary>
/// UI组件注册接口，用于模块化注册Blazor组件
/// </summary>
public interface IUIComponentRegistry
{
    /// <summary>
    /// 注册页面组件
    /// </summary>
    /// <typeparam name="T">组件类型，必须继承自ComponentBase</typeparam>
    /// <param name="route">路由路径</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="icon">图标</param>
    /// <param name="category">分类</param>
    /// <param name="addToNav">是否添加到导航菜单</param>
    /// <param name="navOrder">导航菜单排序顺序</param>
    /// <param name="navLinkMatch">导航链接匹配模式</param>
    void RegisterComponent<T>(string route, string displayName, string? icon = null, string? category = null, bool addToNav = false, int navOrder = 0, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix) where T : ComponentBase;

    /// <summary>
    /// 获取当前注册组件相关的附加的程序集
    /// </summary>
    /// <returns>附加的程序集</returns>
    Assembly[] GetAdditionalAssemblies();

    /// <summary>
    /// 获取所有注册的页面
    /// </summary>
    IReadOnlyList<UIPageInfo> GetRegisteredPages();

    /// <summary>
    /// 获取所有注册的导航项
    /// </summary>
    IReadOnlyList<UINavItem> GetNavItems();

    /// <summary>
    /// 获取注册的组件类型
    /// </summary>
    /// <param name="name">组件名称</param>
    Type? GetComponentType(string name);
} 