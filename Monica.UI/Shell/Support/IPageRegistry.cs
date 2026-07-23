using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Monica.Core.Localization.Abstractions;
using Monica.UI.Localization;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Registers Blazor pages contributed by Monica UI modules.
/// </summary>
public interface IPageRegistry
{
    /// <summary>
    /// Registers a page with literal display text.
    /// </summary>
    /// <typeparam name="TComponent">The page component type.</typeparam>
    /// <param name="route">The route handled by the component.</param>
    /// <param name="displayName">The display name used for the page and optional navigation entry.</param>
    /// <param name="icon">The optional navigation icon.</param>
    /// <param name="category">The optional navigation category.</param>
    /// <param name="addToNav">Whether to add a navigation entry.</param>
    /// <param name="navOrder">The navigation sort order.</param>
    /// <param name="navLinkMatch">The route-matching behavior for the navigation entry.</param>
    void RegisterComponent<TComponent>(
        string route,
        string displayName,
        string? icon = null,
        string? category = null,
        bool addToNav = false,
        int navOrder = 0,
        NavLinkMatch navLinkMatch = NavLinkMatch.Prefix)
        where TComponent : ComponentBase;

    /// <summary>
    /// Registers a page localized through Monica's built-in <see cref="UIRegistryResource"/>.
    /// </summary>
    /// <typeparam name="TComponent">The page component type.</typeparam>
    /// <param name="route">The route handled by the component.</param>
    /// <param name="displayNameKey">The localization key for the display name.</param>
    /// <param name="icon">The optional navigation icon.</param>
    /// <param name="categoryKey">The optional localization key for the navigation category.</param>
    /// <param name="addToNav">Whether to add a navigation entry.</param>
    /// <param name="navOrder">The navigation sort order.</param>
    /// <param name="navLinkMatch">The route-matching behavior for the navigation entry.</param>
    void RegisterLocalizedComponent<TComponent>(
        string route,
        string displayNameKey,
        string? icon = null,
        string? categoryKey = null,
        bool addToNav = false,
        int navOrder = 0,
        NavLinkMatch navLinkMatch = NavLinkMatch.Prefix)
        where TComponent : ComponentBase;

    /// <summary>
    /// Registers a page localized through a module-owned resource marker type.
    /// </summary>
    /// <remarks>
    /// The contributing module must register <typeparamref name="TResource"/> through
    /// <see cref="Monica.Modules.ModuleLocalizationGuide.AddResource{TResource}"/>. Text is resolved from the current host's
    /// <see cref="ILocalizationCatalog"/> when the navigation UI is rendered.
    /// </remarks>
    /// <typeparam name="TComponent">The page component type.</typeparam>
    /// <typeparam name="TResource">The localization resource marker owned by the contributing module.</typeparam>
    /// <param name="route">The route handled by the component.</param>
    /// <param name="displayNameKey">The localization key for the display name.</param>
    /// <param name="icon">The optional navigation icon.</param>
    /// <param name="categoryKey">The optional localization key for the navigation category.</param>
    /// <param name="addToNav">Whether to add a navigation entry.</param>
    /// <param name="navOrder">The navigation sort order.</param>
    /// <param name="navLinkMatch">The route-matching behavior for the navigation entry.</param>
    void RegisterLocalizedComponent<TComponent, TResource>(
        string route,
        string displayNameKey,
        string? icon = null,
        string? categoryKey = null,
        bool addToNav = false,
        int navOrder = 0,
        NavLinkMatch navLinkMatch = NavLinkMatch.Prefix)
        where TComponent : ComponentBase
        where TResource : class, ILocalizationResource;

    /// <summary>
    /// Gets assemblies containing registered pages that the Blazor router must discover.
    /// </summary>
    /// <returns>Additional assemblies</returns>
    Assembly[] GetAdditionalAssemblies();

    /// <summary>
    /// Gets all registered pages.
    /// </summary>
    IReadOnlyList<PageDefinition> GetRegisteredPages();

    /// <summary>
    /// Gets all registered navigation items in configured order.
    /// </summary>
    IReadOnlyList<NavigationItem> GetNavItems();

    /// <summary>
    /// Gets the component registered for a route.
    /// </summary>
    /// <param name="name">The normalized route.</param>
    Type? GetComponentType(string name);
} 
