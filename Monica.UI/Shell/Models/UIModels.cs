using Microsoft.AspNetCore.Components.Routing;

namespace Monica.UI.Shell.Models;

/// <summary>
/// UI page information
/// </summary>
public class PageDefinition
{
    /// <summary>
    /// routing path
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Component type
    /// </summary>
    public required Type ComponentType { get; init; }

    /// <summary>
    /// display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// icon
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Classification
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// localization key for display name
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// Classification localization key
    /// </summary>
    public string? CategoryKey { get; init; }
}

/// <summary>
/// Navigation menu items
/// </summary>
public class NavigationItem
{
    /// <summary>
    /// display text
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Link address
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// icon
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Whether to expand (for menus with sub-items)
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Navigation item classification
    /// </summary>
    public string? Category { get; set; }
    /// <summary>
    /// submenu item
    /// </summary>
    public List<NavigationItem> Children { get; init; } = new();

    /// <summary>
    /// click event
    /// </summary>
    public Action? OnClick { get; init; }

    /// <summary>
    /// Whether to disable
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// sort order
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Navigation link matching pattern
    /// </summary>
    public NavLinkMatch NavLinkMatch { get; init; } = NavLinkMatch.Prefix;

    /// <summary>
    /// Display localization key for text
    /// </summary>
    public string? TextKey { get; init; }

    /// <summary>
    /// Classification localization key
    /// </summary>
    public string? CategoryKey { get; init; }
} 