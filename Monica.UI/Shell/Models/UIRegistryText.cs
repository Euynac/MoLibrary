using Monica.Core.Localization.Abstractions;

namespace Monica.UI.Shell.Models;

/// <summary>
/// Represents literal or resource-backed text contributed to the Monica UI registry.
/// </summary>
public sealed record UIRegistryText
{
    private UIRegistryText(string fallback, string? key, Type? resourceType)
    {
        Fallback = fallback;
        Key = key;
        ResourceType = resourceType;
    }

    /// <summary>
    /// Gets the text returned when the value is literal or the localization key is unavailable.
    /// </summary>
    public string Fallback { get; }

    /// <summary>
    /// Gets the localization key, or <see langword="null"/> for literal text.
    /// </summary>
    public string? Key { get; }

    /// <summary>
    /// Gets the localization resource marker type, or <see langword="null"/> for literal text.
    /// </summary>
    public Type? ResourceType { get; }

    /// <summary>
    /// Creates text that does not require localization.
    /// </summary>
    /// <param name="text">The literal text.</param>
    /// <returns>A literal registry-text value.</returns>
    public static UIRegistryText Literal(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UIRegistryText(text, key: null, resourceType: null);
    }

    /// <summary>
    /// Creates text resolved from a module-owned localization resource.
    /// </summary>
    /// <typeparam name="TResource">The localization resource marker type.</typeparam>
    /// <param name="key">The localization key.</param>
    /// <param name="fallback">The fallback text. When omitted, <paramref name="key"/> is used.</param>
    /// <returns>A resource-backed registry-text value.</returns>
    public static UIRegistryText Localized<TResource>(string key, string? fallback = null)
        where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new UIRegistryText(fallback ?? key, key, typeof(TResource));
    }

    /// <summary>
    /// Resolves this value through the localization catalog owned by the current host.
    /// </summary>
    /// <param name="localizationCatalog">The current host's localization catalog.</param>
    /// <returns>The localized text when found; otherwise <see cref="Fallback"/>.</returns>
    public string Resolve(ILocalizationCatalog localizationCatalog)
    {
        ArgumentNullException.ThrowIfNull(localizationCatalog);

        if (ResourceType is null || Key is null)
        {
            return Fallback;
        }

        var localized = localizationCatalog.For(ResourceType)[Key];
        return localized.ResourceNotFound ? Fallback : localized.Value;
    }

    /// <inheritdoc />
    public override string ToString() => Fallback;
}
