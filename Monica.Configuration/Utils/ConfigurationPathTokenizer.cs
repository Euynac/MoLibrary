using Monica.Configuration.Models;

namespace Monica.Configuration.Utils;

/// <summary>
/// Provides tokenization helpers for logical paths.
/// </summary>
public static class ConfigurationPathTokenizer
{
    /// <summary>
    /// Gets a path's ancestor paths from nearest to farthest.
    /// </summary>
    /// <param name="path">The logical path.</param>
    /// <returns>The ancestor paths.</returns>
    public static IEnumerable<LogicalPath> GetAncestors(LogicalPath path)
    {
        for (var depth = path.Depth - 1; depth >= 0; depth--)
        {
            yield return new LogicalPath(path.Segments.Take(depth).ToArray());
        }
    }

    /// <summary>
    /// Checks whether a path starts with the supplied ancestor path.
    /// </summary>
    /// <param name="path">The candidate path.</param>
    /// <param name="ancestor">The ancestor path.</param>
    /// <returns>True when the path is under the ancestor.</returns>
    public static bool StartsWith(LogicalPath path, LogicalPath ancestor)
    {
        if (ancestor.Depth > path.Depth)
        {
            return false;
        }

        return !ancestor.Segments.Where((segment, index) => segment != path.Segments[index]).Any();
    }
}
