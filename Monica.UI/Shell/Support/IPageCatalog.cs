using System.Reflection;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Exposes the immutable page and navigation catalog composed by the Monica UI shell at startup.
/// </summary>
/// <remarks>
/// The shell seals all <see cref="INavigationRegistryBuilder"/> contributions at the beginning of endpoint
/// configuration and publishes one stable snapshot for the lifetime of the host. Calls made before that point fail;
/// normal Razor component rendering occurs after sealing and can safely read this catalog. Returned collections are
/// read-only snapshots and must not be used as a registration surface.
/// </remarks>
public interface IPageCatalog
{
    /// <summary>
    /// Gets assemblies containing registered pages that the Blazor router must discover.
    /// </summary>
    /// <returns>A new array in deterministic assembly-name order.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called before the UI shell seals startup registrations during endpoint configuration.
    /// </exception>
    Assembly[] GetAdditionalAssemblies();

    /// <summary>
    /// Gets all registered pages in deterministic route order.
    /// </summary>
    /// <returns>The immutable page snapshot for the current host.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called before the UI shell seals startup registrations during endpoint configuration.
    /// </exception>
    IReadOnlyList<PageDefinition> GetRegisteredPages();

    /// <summary>
    /// Gets all registered navigation items in deterministic configured order.
    /// </summary>
    /// <returns>The immutable navigation-item snapshot for the current host.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called before the UI shell seals startup registrations during endpoint configuration.
    /// </exception>
    IReadOnlyList<NavigationItem> GetNavItems();

    /// <summary>
    /// Gets all category definitions in deterministic configured order.
    /// </summary>
    /// <returns>The immutable navigation-category snapshot for the current host.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called before the UI shell seals startup registrations during endpoint configuration.
    /// </exception>
    IReadOnlyList<NavigationCategoryDefinition> GetNavigationCategories();
}
