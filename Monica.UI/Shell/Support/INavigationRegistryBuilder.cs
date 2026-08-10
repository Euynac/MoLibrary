using Microsoft.AspNetCore.Components;
using Monica.Core.Localization.Abstractions;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Collects startup-only Blazor page and navigation contributions from Monica UI modules.
/// </summary>
/// <remarks>
/// The shell passes this write-only contract to module-owned UI registration callbacks during application startup before
/// routing. Contributors cannot inspect or seal the catalog, so one module cannot accidentally prevent later modules
/// from registering their contributions. The shell seals the registry when endpoint configuration begins; callers
/// must not retain this writer or attempt to mutate it after their callback returns.
/// </remarks>
public interface INavigationRegistryBuilder
{
    /// <summary>
    /// Registers a category with literal display text.
    /// </summary>
    /// <param name="categoryId">The durable category identifier.</param>
    /// <param name="displayName">The category label.</param>
    /// <param name="order">The category order. Lower values render first.</param>
    /// <returns>The validated category identifier for use by page registrations.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="categoryId"/> is not a valid category identifier or
    /// <paramref name="displayName"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registry has been sealed, or when the identifier was already registered with a different
    /// label or order.
    /// </exception>
    NavigationCategoryId RegisterCategory(string categoryId, string displayName, int order = 0);

    /// <summary>
    /// Registers a category localized through a module-owned resource marker type.
    /// </summary>
    /// <typeparam name="TResource">The localization resource marker owned by the contributing module.</typeparam>
    /// <param name="categoryId">The durable category identifier.</param>
    /// <param name="displayNameKey">The localization key for the category label.</param>
    /// <param name="order">The category order. Lower values render first.</param>
    /// <returns>The validated category identifier for use by page registrations.</returns>
    /// <remarks>
    /// The contributing module must add <typeparamref name="TResource"/> to the Localization module's resource catalog.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="categoryId"/> is not a valid category identifier or
    /// <paramref name="displayNameKey"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registry has been sealed, or when the identifier was already registered with a different
    /// localization resource, key, or order.
    /// </exception>
    NavigationCategoryId RegisterLocalizedCategory<TResource>(
        string categoryId,
        string displayNameKey,
        int order = 0)
        where TResource : class, ILocalizationResource;

    /// <summary>
    /// Registers a page with literal display text.
    /// </summary>
    /// <typeparam name="TPage">The page component type.</typeparam>
    /// <param name="route">The route handled by the component.</param>
    /// <param name="displayName">The display name used for the page and optional navigation entry.</param>
    /// <param name="icon">The optional navigation icon.</param>
    /// <param name="categoryId">The optional stable navigation category identifier.</param>
    /// <param name="addToNav">Whether to add a navigation entry.</param>
    /// <param name="navOrder">The navigation sort order within its category.</param>
    /// <param name="accessPolicyType">
    /// An optional concrete <see cref="IPageAccessPolicy"/> implementation registered in the current host.
    /// The same policy controls navigation visibility and must be evaluated by the page before protected work begins.
    /// </param>
    /// <remarks>
    /// Routes are trimmed and compared without regard to case. When <paramref name="addToNav"/> is
    /// <see langword="true"/>, <paramref name="categoryId"/> must identify a built-in or registered category before
    /// startup registration is sealed; otherwise endpoint configuration fails.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="route"/> or <paramref name="displayName"/> is null, empty, or whitespace, or when
    /// <paramref name="accessPolicyType"/> is not a concrete <see cref="IPageAccessPolicy"/> implementation.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registry has been sealed or the normalized route is already registered.
    /// </exception>
    void RegisterPage<TPage>(
        string route,
        string displayName,
        string? icon = null,
        NavigationCategoryId? categoryId = null,
        bool addToNav = false,
        int navOrder = 0,
        Type? accessPolicyType = null)
        where TPage : ComponentBase;

    /// <summary>
    /// Registers a page localized through a module-owned resource marker type.
    /// </summary>
    /// <typeparam name="TPage">The page component type.</typeparam>
    /// <typeparam name="TResource">The localization resource marker owned by the contributing module.</typeparam>
    /// <param name="route">The route handled by the component.</param>
    /// <param name="displayNameKey">The localization key for the display name.</param>
    /// <param name="icon">The optional navigation icon.</param>
    /// <param name="categoryId">The optional stable navigation category identifier.</param>
    /// <param name="addToNav">Whether to add a navigation entry.</param>
    /// <param name="navOrder">The navigation sort order within its category.</param>
    /// <param name="accessPolicyType">
    /// An optional concrete <see cref="IPageAccessPolicy"/> implementation registered in the current host.
    /// Missing policy registration denies access.
    /// </param>
    /// <remarks>
    /// The contributing module must add <typeparamref name="TResource"/> to the Localization module's resource catalog.
    /// Text is resolved from the current
    /// host's <see cref="ILocalizationCatalog"/> when the navigation UI is rendered. Routes are trimmed and compared
    /// without regard to case. When <paramref name="addToNav"/> is <see langword="true"/>,
    /// <paramref name="categoryId"/> must identify a built-in or registered category before startup registration is
    /// sealed; otherwise endpoint configuration fails.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="route"/> or <paramref name="displayNameKey"/> is null, empty, or whitespace, or when
    /// <paramref name="accessPolicyType"/> is not a concrete <see cref="IPageAccessPolicy"/> implementation.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registry has been sealed or the normalized route is already registered.
    /// </exception>
    void RegisterLocalizedPage<TPage, TResource>(
        string route,
        string displayNameKey,
        string? icon = null,
        NavigationCategoryId? categoryId = null,
        bool addToNav = false,
        int navOrder = 0,
        Type? accessPolicyType = null)
        where TPage : ComponentBase
        where TResource : class, ILocalizationResource;
}
