namespace Monica.UI.Shell.Support;

/// <summary>
/// Matches a shell navigation destination against the current base-relative route.
/// </summary>
internal static class NavigationRouteMatcher
{
    /// <summary>
    /// Gets the CSS class used to identify an active navigation destination.
    /// </summary>
    /// <param name="currentRoute">The current base-relative route.</param>
    /// <param name="destination">The registered navigation destination.</param>
    /// <returns><c>active</c> when the destination owns the current route; otherwise, <c>null</c>.</returns>
    internal static string? GetActiveClass(string currentRoute, string destination)
    {
        return IsActive(currentRoute, destination) ? "active" : null;
    }

    /// <summary>
    /// Determines whether the current route is the destination or one of its child routes.
    /// </summary>
    /// <param name="currentRoute">The current base-relative route, optionally including a query or fragment.</param>
    /// <param name="destination">The registered navigation destination.</param>
    /// <returns><c>true</c> when the destination owns the current route.</returns>
    internal static bool IsActive(string currentRoute, string destination)
    {
        var normalizedCurrentRoute = Normalize(currentRoute);
        var normalizedDestination = Normalize(destination);

        if (normalizedDestination.Length == 0)
        {
            return normalizedCurrentRoute.Length == 0;
        }

        return normalizedCurrentRoute.Equals(normalizedDestination, StringComparison.OrdinalIgnoreCase)
            || normalizedCurrentRoute.StartsWith($"{normalizedDestination}/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string route)
    {
        var suffixIndex = route.IndexOfAny(['?', '#']);
        var path = suffixIndex >= 0 ? route[..suffixIndex] : route;
        return path.Trim('/');
    }
}
