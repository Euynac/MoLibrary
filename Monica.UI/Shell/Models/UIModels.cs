using Monica.Core.Localization.Abstractions;

namespace Monica.UI.Shell.Models;

/// <summary>
/// Describes a page contributed to the Monica UI shell.
/// </summary>
public sealed class PageDefinition
{
    internal PageDefinition(string route, Type componentType, UIRegistryText displayName)
    {
        Route = route;
        ComponentType = componentType;
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the normalized route without leading or trailing slashes.
    /// </summary>
    public string Route { get; }

    /// <summary>
    /// Gets the Blazor component rendered for <see cref="Route"/>.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets the display text and its optional localization metadata.
    /// </summary>
    public UIRegistryText DisplayName { get; }

    /// <summary>
    /// Resolves the page display name through the specified host localization catalog.
    /// </summary>
    /// <param name="localizationCatalog">The localization catalog owned by the current host.</param>
    /// <returns>The localized value, or the registered fallback when no localized value exists.</returns>
    public string ResolveDisplayName(ILocalizationCatalog localizationCatalog)
    {
        return DisplayName.Resolve(localizationCatalog);
    }

}

/// <summary>
/// Describes one navigation entry contributed to the Monica UI shell.
/// </summary>
public sealed class NavigationItem
{
    internal NavigationItem(
        UIRegistryText text,
        string href,
        string? icon,
        NavigationCategoryId categoryId,
        int order)
    {
        Text = text;
        Href = href;
        Icon = icon;
        CategoryId = categoryId;
        Order = order;
    }

    /// <summary>
    /// Gets the label text and its optional localization metadata.
    /// </summary>
    public UIRegistryText Text { get; }

    /// <summary>
    /// Gets the target route or URI.
    /// </summary>
    public string Href { get; }

    /// <summary>
    /// Gets the optional icon identifier shown by the shell.
    /// </summary>
    public string? Icon { get; }

    /// <summary>
    /// Gets the stable category identity used to group this navigation entry.
    /// </summary>
    public NavigationCategoryId CategoryId { get; }

    /// <summary>
    /// Gets the navigation sort order.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Resolves the navigation label through the specified host localization catalog.
    /// </summary>
    /// <param name="localizationCatalog">The localization catalog owned by the current host.</param>
    /// <returns>The localized label, or the registered fallback when no localized value exists.</returns>
    public string ResolveText(ILocalizationCatalog localizationCatalog)
    {
        return Text.Resolve(localizationCatalog);
    }

}
