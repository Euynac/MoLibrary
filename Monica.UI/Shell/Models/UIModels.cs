using Microsoft.AspNetCore.Components.Routing;
using Monica.Core.Localization.Abstractions;

namespace Monica.UI.Shell.Models;

/// <summary>
/// Describes a page contributed to the Monica UI shell.
/// </summary>
public class PageDefinition
{
    /// <summary>
    /// Gets the normalized route without leading or trailing slashes.
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Gets the Blazor component rendered for <see cref="Route"/>.
    /// </summary>
    public required Type ComponentType { get; init; }

    /// <summary>
    /// Gets the display text and its optional localization metadata.
    /// </summary>
    public required UIRegistryText DisplayName { get; init; }

    /// <summary>
    /// Gets the optional icon identifier shown by the shell.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the optional category text and its localization metadata.
    /// </summary>
    public UIRegistryText? Category { get; init; }

    /// <summary>
    /// Resolves the page display name through the specified host localization catalog.
    /// </summary>
    /// <param name="localizationCatalog">The localization catalog owned by the current host.</param>
    /// <returns>The localized value, or the registered fallback when no localized value exists.</returns>
    public string ResolveDisplayName(ILocalizationCatalog localizationCatalog)
    {
        return DisplayName.Resolve(localizationCatalog);
    }

    /// <summary>
    /// Resolves the optional page category through the specified host localization catalog.
    /// </summary>
    /// <param name="localizationCatalog">The localization catalog owned by the current host.</param>
    /// <returns>The localized category, its fallback value, or <see langword="null"/> when no category is configured.</returns>
    public string? ResolveCategory(ILocalizationCatalog localizationCatalog)
    {
        return Category?.Resolve(localizationCatalog);
    }
}

/// <summary>
/// Describes one navigation entry contributed to the Monica UI shell.
/// </summary>
public class NavigationItem
{
    /// <summary>
    /// Gets the label text and its optional localization metadata.
    /// </summary>
    public required UIRegistryText Text { get; init; }

    /// <summary>
    /// Gets the target route or URI.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// Gets the optional icon identifier shown by the shell.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets or sets whether a navigation group is expanded.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Gets the optional category text and its localization metadata.
    /// </summary>
    public UIRegistryText? Category { get; init; }

    /// <summary>
    /// Gets nested navigation entries.
    /// </summary>
    public List<NavigationItem> Children { get; init; } = [];

    /// <summary>
    /// Gets an optional callback invoked by a programmatic navigation entry.
    /// </summary>
    public Action? OnClick { get; init; }

    /// <summary>
    /// Gets whether the entry is unavailable for interaction.
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// Gets the navigation sort order.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the route-matching behavior used by the navigation link.
    /// </summary>
    public NavLinkMatch NavLinkMatch { get; init; } = NavLinkMatch.Prefix;

    /// <summary>
    /// Resolves the navigation label through the specified host localization catalog.
    /// </summary>
    /// <param name="localizationCatalog">The localization catalog owned by the current host.</param>
    /// <returns>The localized label, or the registered fallback when no localized value exists.</returns>
    public string ResolveText(ILocalizationCatalog localizationCatalog)
    {
        return Text.Resolve(localizationCatalog);
    }

    /// <summary>
    /// Resolves the optional navigation category through the specified host localization catalog.
    /// </summary>
    /// <param name="localizationCatalog">The localization catalog owned by the current host.</param>
    /// <returns>The localized category, its fallback value, or <see langword="null"/> when no category is configured.</returns>
    public string? ResolveCategory(ILocalizationCatalog localizationCatalog)
    {
        return Category?.Resolve(localizationCatalog);
    }
}
