using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Monica.Core.Localization.Abstractions;
using Monica.UI.Localization;
using Monica.UI.Shell.Components;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Stores the page and navigation contributions for one Monica host.
/// </summary>
public class PageRegistry : IPageRegistry
{
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PageDefinition> _pages = [];
    private readonly List<NavigationItem> _navItems = [];

    /// <summary>
    /// Already registered assemblies cannot be registered again
    /// </summary>
    private readonly HashSet<Assembly> _excludedAssemblies = [typeof(AppRouter).Assembly];

    /// <inheritdoc />
    public void RegisterComponent<TComponent>(
        string route,
        string displayName,
        string? icon = null,
        string? category = null,
        bool addToNav = false,
        int navOrder = 0,
        NavLinkMatch navLinkMatch = NavLinkMatch.Prefix)
        where TComponent : ComponentBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        RegisterPage(
            new PageDefinition
            {
                Route = NormalizeRoute(route),
                ComponentType = typeof(TComponent),
                DisplayName = UIRegistryText.Literal(displayName),
                Icon = icon,
                Category = category is null ? null : UIRegistryText.Literal(category)
            },
            addToNav,
            navOrder,
            navLinkMatch);
    }

    /// <inheritdoc />
    public void RegisterLocalizedComponent<TComponent>(
        string route,
        string displayNameKey,
        string? icon = null,
        string? categoryKey = null,
        bool addToNav = false,
        int navOrder = 0,
        NavLinkMatch navLinkMatch = NavLinkMatch.Prefix)
        where TComponent : ComponentBase
    {
        RegisterLocalizedComponent<TComponent, UIRegistryResource>(
            route,
            displayNameKey,
            icon,
            categoryKey,
            addToNav,
            navOrder,
            navLinkMatch);
    }

    /// <inheritdoc />
    public void RegisterLocalizedComponent<TComponent, TResource>(
        string route,
        string displayNameKey,
        string? icon = null,
        string? categoryKey = null,
        bool addToNav = false,
        int navOrder = 0,
        NavLinkMatch navLinkMatch = NavLinkMatch.Prefix)
        where TComponent : ComponentBase
        where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayNameKey);

        RegisterPage(
            new PageDefinition
            {
                Route = NormalizeRoute(route),
                ComponentType = typeof(TComponent),
                DisplayName = UIRegistryText.Localized<TResource>(displayNameKey),
                Icon = icon,
                Category = categoryKey is null ? null : UIRegistryText.Localized<TResource>(categoryKey)
            },
            addToNav,
            navOrder,
            navLinkMatch);
    }

    /// <inheritdoc />
    public IReadOnlyList<PageDefinition> GetRegisteredPages()
    {
        return _pages.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<NavigationItem> GetNavItems()
    {
        return _navItems.OrderBy(static item => item.Order).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public Type? GetComponentType(string name)
    {
        return _components.GetValueOrDefault(name);
    }

    /// <inheritdoc />
    public Assembly[] GetAdditionalAssemblies()
    {
        return _assemblies.ToArray();
    }

    private static string NormalizeRoute(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        return route.Trim().Trim('/');
    }

    private void RegisterPage(
        PageDefinition page,
        bool addToNav,
        int navOrder,
        NavLinkMatch navLinkMatch)
    {
        if (_components.TryGetValue(page.Route, out var existingComponentType))
        {
            throw new InvalidOperationException(
                $"UI route '/{page.Route}' is already registered by component " +
                $"'{existingComponentType.FullName}' and cannot also map to '{page.ComponentType.FullName}'.");
        }

        _pages.Add(page);
        _components[page.Route] = page.ComponentType;

        if (addToNav)
        {
            _navItems.Add(new NavigationItem
            {
                Text = page.DisplayName,
                Href = page.Route,
                Icon = page.Icon,
                Category = page.Category,
                Order = navOrder,
                NavLinkMatch = navLinkMatch
            });
        }

        if (!_excludedAssemblies.Contains(page.ComponentType.Assembly))
        {
            _assemblies.Add(page.ComponentType.Assembly);
        }
    }
}
