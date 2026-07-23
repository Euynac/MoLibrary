using System.Reflection;
using Microsoft.AspNetCore.Components;
using Monica.Core.Localization.Abstractions;
using Monica.UI.Localization;
using Monica.UI.Shell.Components;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Stores startup-only page, category, and navigation contributions for one Monica host.
/// </summary>
internal sealed class PageRegistry : INavigationRegistryBuilder, IPageCatalog
{
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly Dictionary<NavigationCategoryId, NavigationCategoryDefinition> _categories = [];
    private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Assembly> _excludedAssemblies = [typeof(AppRouter).Assembly];
    private readonly List<NavigationItem> _navItems = [];
    private readonly List<PageDefinition> _pages = [];
    private readonly object _sync = new();
    private Snapshot? _snapshot;

    /// <summary>
    /// Initializes a registry with the shared category taxonomy owned by the Monica UI shell.
    /// </summary>
    public PageRegistry()
    {
        AddBuiltInCategory(BuiltInNavigationCategoryIds.AI, "NavBar:Categories:AI", 100);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.KnowledgeRetrieval, "NavBar:Categories:KnowledgeRetrieval", 200);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.Documentation, "NavBar:Categories:Documentation", 300);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.Configuration, "NavBar:Categories:Configuration", 400);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.Infrastructure, "NavBar:Categories:Infrastructure", 500);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.Module, "NavBar:Categories:Module", 600);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.TaskScheduling, "NavBar:Categories:TaskScheduling", 700);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.Monitor, "NavBar:Categories:Monitor", 800);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.Debug, "NavBar:Categories:Debug", 900);
        AddBuiltInCategory(BuiltInNavigationCategoryIds.Uncategorized, "NavBar:Categories:Uncategorized", 1_000);
    }

    /// <inheritdoc />
    public NavigationCategoryId RegisterCategory(string categoryId, string displayName, int order = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return RegisterCategoryCore(
            new NavigationCategoryDefinition(
                NavigationCategoryId.Create(categoryId),
                UIRegistryText.Literal(displayName),
                order));
    }

    /// <inheritdoc />
    public NavigationCategoryId RegisterLocalizedCategory<TResource>(
        string categoryId,
        string displayNameKey,
        int order = 0)
        where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayNameKey);
        return RegisterCategoryCore(
            new NavigationCategoryDefinition(
                NavigationCategoryId.Create(categoryId),
                UIRegistryText.Localized<TResource>(displayNameKey),
                order));
    }

    /// <inheritdoc />
    public void RegisterPage<TPage>(
        string route,
        string displayName,
        string? icon = null,
        NavigationCategoryId? categoryId = null,
        bool addToNav = false,
        int navOrder = 0)
        where TPage : ComponentBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        RegisterPageCore(
            new PageDefinition(
                NormalizeRoute(route),
                typeof(TPage),
                UIRegistryText.Literal(displayName)),
            icon,
            categoryId,
            addToNav,
            navOrder);
    }

    /// <inheritdoc />
    public void RegisterLocalizedPage<TPage, TResource>(
        string route,
        string displayNameKey,
        string? icon = null,
        NavigationCategoryId? categoryId = null,
        bool addToNav = false,
        int navOrder = 0)
        where TPage : ComponentBase
        where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayNameKey);

        RegisterPageCore(
            new PageDefinition(
                NormalizeRoute(route),
                typeof(TPage),
                UIRegistryText.Localized<TResource>(displayNameKey)),
            icon,
            categoryId,
            addToNav,
            navOrder);
    }

    /// <inheritdoc />
    public IReadOnlyList<PageDefinition> GetRegisteredPages() => GetSnapshot().Pages;

    /// <inheritdoc />
    public IReadOnlyList<NavigationItem> GetNavItems() => GetSnapshot().NavigationItems;

    /// <inheritdoc />
    public IReadOnlyList<NavigationCategoryDefinition> GetNavigationCategories() => GetSnapshot().Categories;

    /// <inheritdoc />
    public Assembly[] GetAdditionalAssemblies()
    {
        return GetSnapshot().Assemblies.ToArray();
    }

    internal void Seal()
    {
        lock (_sync)
        {
            if (_snapshot is not null)
            {
                return;
            }

            var missingCategoryIds = _navItems
                .Select(static item => item.CategoryId)
                .Distinct()
                .Where(categoryId => !_categories.ContainsKey(categoryId))
                .OrderBy(static categoryId => categoryId.Value, StringComparer.Ordinal)
                .ToArray();

            if (missingCategoryIds.Length > 0)
            {
                throw new InvalidOperationException(
                    "Navigation entries reference unregistered categories: " +
                    string.Join(", ", missingCategoryIds.Select(static categoryId => $"'{categoryId}'")) + ".");
            }

            _snapshot = new Snapshot(
                _assemblies.OrderBy(static assembly => assembly.FullName, StringComparer.Ordinal).ToArray(),
                _pages
                    .OrderBy(static page => page.Route, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static page => page.ComponentType.FullName, StringComparer.Ordinal)
                    .ToList()
                    .AsReadOnly(),
                _navItems
                    .OrderBy(static item => item.Order)
                    .ThenBy(static item => item.Href, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly(),
                _categories.Values
                    .OrderBy(static category => category.Order)
                    .ThenBy(static category => category.Id.Value, StringComparer.Ordinal)
                    .ToList()
                    .AsReadOnly());
        }
    }

    private static string NormalizeRoute(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        return route.Trim().Trim('/');
    }

    private void AddBuiltInCategory(NavigationCategoryId id, string displayNameKey, int order)
    {
        _categories.Add(
            id,
            new NavigationCategoryDefinition(id, UIRegistryText.Localized<SharedResource>(displayNameKey), order));
    }

    private NavigationCategoryId RegisterCategoryCore(NavigationCategoryDefinition category)
    {
        lock (_sync)
        {
            EnsureMutable();

            if (!_categories.TryGetValue(category.Id, out var existing))
            {
                _categories.Add(category.Id, category);
                return category.Id;
            }

            if (existing.HasSameRegistrationAs(category))
            {
                return existing.Id;
            }

            throw new InvalidOperationException(
                $"Navigation category '{existing.Id}' is already registered with a different label, resource, or order.");
        }
    }

    private void RegisterPageCore(
        PageDefinition page,
        string? icon,
        NavigationCategoryId? categoryId,
        bool addToNav,
        int navOrder)
    {
        lock (_sync)
        {
            EnsureMutable();

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
                _navItems.Add(new NavigationItem(
                    page.DisplayName,
                    page.Route,
                    icon,
                    categoryId ?? BuiltInNavigationCategoryIds.Uncategorized,
                    navOrder));
            }

            if (!_excludedAssemblies.Contains(page.ComponentType.Assembly))
            {
                _assemblies.Add(page.ComponentType.Assembly);
            }
        }
    }

    private Snapshot GetSnapshot()
    {
        lock (_sync)
        {
            return _snapshot ?? throw new InvalidOperationException(
                "The page catalog is not available until the UI shell completes startup registration.");
        }
    }

    private void EnsureMutable()
    {
        if (_snapshot is not null)
        {
            throw new InvalidOperationException(
                "The page registry has already been sealed. Register pages and categories during application startup.");
        }
    }

    private sealed record Snapshot(
        IReadOnlyList<Assembly> Assemblies,
        IReadOnlyList<PageDefinition> Pages,
        IReadOnlyList<NavigationItem> NavigationItems,
        IReadOnlyList<NavigationCategoryDefinition> Categories);
}
