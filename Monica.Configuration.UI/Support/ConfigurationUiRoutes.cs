using Microsoft.AspNetCore.WebUtilities;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Builds and parses Monica.Configuration.UI routes and query parameters.
/// </summary>
internal static class ConfigurationUiRoutes
{
    private const string PATH_QUERY_KEY = "path";

    /// <summary>
    /// Gets the overview route.
    /// </summary>
    public const string OVERVIEW_ROUTE = "/configuration";

    /// <summary>
    /// Builds the definition detail route.
    /// </summary>
    public static string Definition(string definitionKey)
    {
        return $"/configuration/{Uri.EscapeDataString(definitionKey)}";
    }

    /// <summary>
    /// Builds the value detail route.
    /// </summary>
    public static string Value(string definitionKey, LogicalPath? logicalPath = null)
    {
        return WithPath($"{Definition(definitionKey)}/value", logicalPath);
    }

    /// <summary>
    /// Builds the mutation editor route.
    /// </summary>
    public static string Edit(string definitionKey, LogicalPath? logicalPath = null)
    {
        return WithPath($"{Definition(definitionKey)}/edit", logicalPath);
    }

    /// <summary>
    /// Builds the history route.
    /// </summary>
    public static string History(string definitionKey, LogicalPath? logicalPath = null)
    {
        return WithPath($"{Definition(definitionKey)}/history", logicalPath);
    }

    /// <summary>
    /// Reads the logical path query parameter from the current URI.
    /// </summary>
    public static LogicalPath ReadPath(string absoluteUri)
    {
        var uri = new Uri(absoluteUri, UriKind.Absolute);
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue(PATH_QUERY_KEY, out var values) || values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
        {
            return LogicalPath.Root;
        }

        return LogicalPath.Parse(values[0]!);
    }

    /// <summary>
    /// Builds a route with an optional logical path query parameter.
    /// </summary>
    public static string WithPath(string route, LogicalPath? logicalPath)
    {
        if (logicalPath is null || logicalPath.Depth == 0)
        {
            return route;
        }

        return QueryHelpers.AddQueryString(route, PATH_QUERY_KEY, logicalPath.ToCanonicalString());
    }
}
