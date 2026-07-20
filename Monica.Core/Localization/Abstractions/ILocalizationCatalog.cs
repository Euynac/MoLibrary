using Microsoft.Extensions.Localization;

namespace Monica.Core.Localization.Abstractions;

/// <summary>
/// Provides host-scoped access to Monica localization resources when constructor injection of
/// <see cref="IStringLocalizer{T}"/> is not practical.
/// </summary>
/// <remarks>
/// The catalog is owned by one dependency-injection container. Localizers returned by one host
/// never share options, resource registrations, caches, or loggers with another host.
/// Prefer injecting <see cref="IStringLocalizer{T}"/> directly in normal application services and UI components.
/// </remarks>
public interface ILocalizationCatalog
{
    /// <summary>
    /// Gets the localizer for a registered localization resource marker type.
    /// </summary>
    /// <typeparam name="TResource">The resource marker type.</typeparam>
    /// <returns>The host-owned localizer for <typeparamref name="TResource"/>.</returns>
    IStringLocalizer For<TResource>() where TResource : class, ILocalizationResource;

    /// <summary>
    /// Gets the localizer for a registered localization resource marker type.
    /// </summary>
    /// <param name="resourceType">The resource marker type.</param>
    /// <returns>The host-owned localizer for <paramref name="resourceType"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="resourceType"/> does not implement <see cref="ILocalizationResource"/>.
    /// </exception>
    IStringLocalizer For(Type resourceType);

    /// <summary>
    /// Resolves a localized string for the specified resource marker type.
    /// </summary>
    /// <typeparam name="TResource">The resource marker type.</typeparam>
    /// <param name="key">The localization key.</param>
    /// <returns>The localized value, or the key when the resource does not contain it.</returns>
    string Get<TResource>(string key) where TResource : class, ILocalizationResource;

    /// <summary>
    /// Resolves and formats a localized string for the specified resource marker type.
    /// </summary>
    /// <typeparam name="TResource">The resource marker type.</typeparam>
    /// <param name="key">The localization key.</param>
    /// <param name="arguments">The formatting arguments.</param>
    /// <returns>The localized formatted value, or the key when the resource does not contain it.</returns>
    string Get<TResource>(string key, params object[] arguments) where TResource : class, ILocalizationResource;
}
