namespace Monica.UI.Shell.Models;

/// <summary>
/// Represents one resolved navigation category and its ordered entries.
/// </summary>
/// <param name="Id">The stable category identity.</param>
/// <param name="DisplayName">The localized category label for the current host.</param>
/// <param name="Items">The navigation entries in configured order.</param>
public sealed record NavigationGroup(
    NavigationCategoryId Id,
    string DisplayName,
    IReadOnlyList<NavigationItem> Items);
