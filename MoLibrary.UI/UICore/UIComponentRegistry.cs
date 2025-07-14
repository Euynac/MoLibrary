using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace MoLibrary.UI.UICore;

/// <summary>
/// UI组件注册服务实现
/// </summary>
public class UIComponentRegistry : IUIComponentRegistry
{
    private readonly HashSet<Assembly> _assemblies = new();
    private readonly Dictionary<string, Type> _components = new();
    private readonly List<UIPageInfo> _pages = new();
    private readonly List<UINavItem> _navItems = new();

    /// <summary>
    /// 注册页面组件
    /// </summary>
    public void RegisterPage(string route, Type componentType, string displayName, string? icon = null, string? category = null)
    {
        if (!typeof(ComponentBase).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"Component type {componentType.Name} must inherit from ComponentBase", nameof(componentType));
        }

        var pageInfo = new UIPageInfo
        {
            Route = route,
            ComponentType = componentType,
            DisplayName = displayName,
            Icon = icon,
            Category = category
        };

        _pages.Add(pageInfo);
    }

    /// <summary>
    /// 注册可重用组件
    /// </summary>
    public void RegisterComponent<T>(string name) where T : ComponentBase
    {
        _components[name] = typeof(T);
    }

    /// <summary>
    /// 注册导航菜单项
    /// </summary>
    public void RegisterNavItem(UINavItem menuItem)
    {
        _navItems.Add(menuItem);
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