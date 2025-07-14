using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace MoLibrary.UI.UICore;

/// <summary>
/// UI组件注册接口，用于模块化注册Blazor组件
/// </summary>
public interface IUIComponentRegistry
{
    /// <summary>
    /// 注册页面组件
    /// </summary>
    /// <param name="route">路由路径</param>
    /// <param name="componentType">组件类型</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="icon">图标</param>
    /// <param name="category">分类</param>
    void RegisterPage(string route, Type componentType, string displayName, string? icon = null, string? category = null);

    /// <summary>
    /// 注册可重用组件
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    /// <param name="name">组件名称</param>
    void RegisterComponent<T>(string name) where T : ComponentBase;

    /// <summary>
    /// 获取当前注册组件相关的附加的程序集
    /// </summary>
    /// <returns>附加的程序集</returns>
    Assembly[] GetAdditionalAssemblies();

    /// <summary>
    /// 注册导航菜单项
    /// </summary>
    /// <param name="menuItem">菜单项</param>
    void RegisterNavItem(UINavItem menuItem);

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