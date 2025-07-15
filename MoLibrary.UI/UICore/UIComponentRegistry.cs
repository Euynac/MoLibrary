using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MoLibrary.UI.Components;

namespace MoLibrary.UI.UICore;

/// <summary>
/// UI组件注册服务实现
/// </summary>
public class UIComponentRegistry : IUIComponentRegistry
{
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly Dictionary<string, Type> _components = new();
    private readonly List<UIPageInfo> _pages = [];
    private readonly List<UINavItem> _navItems = [];

    /// <summary>
    /// 已经注册的程序集不可再注册
    /// </summary>
    private readonly HashSet<Assembly> _excludedAssemblies = [typeof(MoRouter).Assembly];

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
    public void RegisterComponent<T>(string route, string displayName, string? icon = null, string? category = null, bool addToNav = false, int navOrder = 0, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix) where T : ComponentBase
    {
        route = route.TrimStart('/');
        var componentType = typeof(T);
        
        // 注册页面信息
        var pageInfo = new UIPageInfo
        {
            Route = route,
            ComponentType = componentType,
            DisplayName = displayName,
            Icon = icon,
            Category = category
        };
        _pages.Add(pageInfo);

        // 注册组件类型（用于名称查找）
        _components[route] = componentType;

        // 如果需要，自动创建导航菜单项
        if (addToNav)
        {
            var navItem = new UINavItem
            {
                Text = displayName,
                Href = route,
                Icon = icon,
                Order = navOrder,
                NavLinkMatch = navLinkMatch
            };
            _navItems.Add(navItem);
        }

        if (!_excludedAssemblies.Contains(componentType.Assembly))
        {
            // 添加组件所在的程序集
            _assemblies.Add(componentType.Assembly);
        }

    }

    /// <summary>
    /// 获取所有注册的页面
    /// </summary>
    public IReadOnlyList<UIPageInfo> GetRegisteredPages()
    {
        return _pages.AsReadOnly();
    }

    /// <summary>
    /// 获取所有注册的导航项
    /// </summary>
    public IReadOnlyList<UINavItem> GetNavItems()
    {
        return _navItems.OrderBy(x => x.Order).ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取注册的组件类型
    /// </summary>
    public Type? GetComponentType(string name)
    {
        return _components.TryGetValue(name, out var type) ? type : null;
    }

    /// <summary>
    /// 获取当前注册组件相关的附加的程序集
    /// </summary>
    /// <returns>附加的程序集</returns>
    public Assembly[] GetAdditionalAssemblies()
    {
        return _assemblies.ToArray();
    }
} 