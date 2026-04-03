using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// UI component registration interface for modular registration of Blazor components
/// </summary>
public interface IPageRegistry
{
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
    void RegisterComponent<T>(string route, string displayName, string? icon = null, string? category = null, bool addToNav = false, int navOrder = 0, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix) where T : ComponentBase;

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
    void RegisterLocalizedComponent<T>(string route, string displayNameKey, string? icon = null, string? categoryKey = null, bool addToNav = false, int navOrder = 0, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix) where T : ComponentBase;

    /// <summary>
    /// Get additional assemblies related to the currently registered component
    /// </summary>
    /// <returns>Additional assemblies</returns>
    Assembly[] GetAdditionalAssemblies();

    /// <summary>
    /// Get all registered pages
    /// </summary>
    IReadOnlyList<PageDefinition> GetRegisteredPages();

    /// <summary>
    /// Get all registered navigation items
    /// </summary>
    IReadOnlyList<NavigationItem> GetNavItems();

    /// <summary>
    /// Get the registered component type
    /// </summary>
    /// <param name="name">Component name</param>
    Type? GetComponentType(string name);
} 
