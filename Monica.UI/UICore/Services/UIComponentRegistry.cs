using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Monica.UI.Components;
using Monica.UI.UICore.Interfaces;
using Monica.UI.UICore.Models;

namespace Monica.UI.UICore.Services;

/// <summary>
/// UI component registration service implementation
/// </summary>
public class UIComponentRegistry : IUIComponentRegistry
{
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly Dictionary<string, Type> _components = new();
    private readonly List<UIPageInfo> _pages = [];
    private readonly List<UINavItem> _navItems = [];

    /// <summary>
    /// Already registered assemblies cannot be registered again
    /// </summary>
    private readonly HashSet<Assembly> _excludedAssemblies = [typeof(MoRouter).Assembly];

    /// <summary>
    /// Register page components
    /// </summary>
    /// <typeparam name="T">Component type, must inherit from ComponentBase</typeparam>
    /// <param name="route">Route path</param>
    /// <param name="displayName">Display name</param>
    /// <param name="icon">icon</param>
    /// <param name="category">Category</param>
    /// <param name="addToNav">Whether to add to the navigation menu</param>
    /// <param name="navOrder">Navigation menu sort order</param>
    /// <param name="navLinkMatch">Navigation link matching pattern</param>
    public void RegisterComponent<T>(string route, string displayName, string? icon = null, string? category = null, bool addToNav = false, int navOrder = 0, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix) where T : ComponentBase
    {
        route = route.TrimStart('/');
        var componentType = typeof(T);

        // Registration page information
        var pageInfo = new UIPageInfo
        {
            Route = route,
            ComponentType = componentType,
            DisplayName = displayName,
            Icon = icon,
            Category = category
        };
        _pages.Add(pageInfo);

        // Register component type (for name lookup)
        _components[route] = componentType;

        // Automatically create navigation menu items if needed
        if (addToNav)
        {
            var navItem = new UINavItem
            {
                Text = displayName,
                Href = route,
                Icon = icon,
                Category = category,
                Order = navOrder,
                NavLinkMatch = navLinkMatch
            };
            _navItems.Add(navItem);
        }

        if (!_excludedAssemblies.Contains(componentType.Assembly))
        {
            // Add the assembly where the component is located
            _assemblies.Add(componentType.Assembly);
        }

    }

    /// <summary>
    /// Register page components (support localization)
    /// </summary>
    /// <typeparam name="T">Component type, must inherit from ComponentBase</typeparam>
    /// <param name="route">Route path</param>
    /// <param name="displayNameKey">Localized key for display name (using UIRegistryResource)</param>
    /// <param name="icon">icon</param>
    /// <param name="categoryKey">Category localization key (using UIRegistryResource)</param>
    /// <param name="addToNav">Whether to add to the navigation menu</param>
    /// <param name="navOrder">Navigation menu sort order</param>
    /// <param name="navLinkMatch">Navigation link matching pattern</param>
    public void RegisterLocalizedComponent<T>(string route, string displayNameKey, string? icon = null, string? categoryKey = null, bool addToNav = false, int navOrder = 0, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix) where T : ComponentBase
    {
        route = route.TrimStart('/');
        var componentType = typeof(T);

        // Registration page information
        var pageInfo = new UIPageInfo
        {
            Route = route,
            ComponentType = componentType,
            DisplayName = displayNameKey,  // Fallback
            DisplayNameKey = displayNameKey,
            Icon = icon,
            Category = categoryKey,  // Fallback
            CategoryKey = categoryKey
        };
        _pages.Add(pageInfo);

        // Register component type (for name lookup)
        _components[route] = componentType;

        // Automatically create navigation menu items if needed
        if (addToNav)
        {
            var navItem = new UINavItem
            {
                Text = displayNameKey,  // Fallback
                TextKey = displayNameKey,
                Href = route,
                Icon = icon,
                Category = categoryKey,  // Fallback
                CategoryKey = categoryKey,
                Order = navOrder,
                NavLinkMatch = navLinkMatch
            };
            _navItems.Add(navItem);
        }

        if (!_excludedAssemblies.Contains(componentType.Assembly))
        {
            // Add the assembly where the component is located
            _assemblies.Add(componentType.Assembly);
        }
    }

    /// <summary>
    /// Get all registered pages
    /// </summary>
    public IReadOnlyList<UIPageInfo> GetRegisteredPages()
    {
        return _pages.AsReadOnly();
    }

    /// <summary>
    /// Get all registered navigation items
    /// </summary>
    public IReadOnlyList<UINavItem> GetNavItems()
    {
        return _navItems.OrderBy(x => x.Order).ToList().AsReadOnly();
    }

    /// <summary>
    /// Get the registered component type
    /// </summary>
    public Type? GetComponentType(string name)
    {
        return _components.GetValueOrDefault(name);
    }

    /// <summary>
    /// Get additional assemblies related to the currently registered component
    /// </summary>
    /// <returns>Additional assemblies</returns>
    public Assembly[] GetAdditionalAssemblies()
    {
        return _assemblies.ToArray();
    }
} 