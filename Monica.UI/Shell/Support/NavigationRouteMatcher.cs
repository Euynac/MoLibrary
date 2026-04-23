using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Route matching utility for AppBar menu items.
/// Normalizes URIs and determines whether a <see cref="NavigationItem"/> is active based on the current route.
/// </summary>
internal static class NavigationRouteMatcher
{
    /// <summary>
    /// Returns <c>"active"</c> if the navigation item matches the current route, otherwise <c>null</c>.
    /// Intended for binding CSS classes to <c>MudMenuItem</c>.
    /// </summary>
    public static string? GetActiveClass(NavigationManager navigationManager, NavigationItem item) =>
        IsActive(navigationManager, item.Href, item.NavLinkMatch) ? "active" : null;

    public static bool IsActive(NavigationManager navigationManager, string? href, NavLinkMatch match)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var currentPath = Normalize(navigationManager.ToBaseRelativePath(navigationManager.Uri));
        var targetPath = Normalize(href);

        if (targetPath.Length == 0)
        {
            return currentPath.Length == 0;
        }

        return match == NavLinkMatch.All
            ? string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase)
            : string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase)
              || currentPath.StartsWith($"{targetPath}/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        var trimmedPath = path.Split(['?', '#'], 2)[0];
        return trimmedPath.Trim('/');
    }
}
