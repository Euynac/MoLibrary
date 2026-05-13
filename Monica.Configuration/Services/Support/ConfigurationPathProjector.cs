using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Projects logical paths into Microsoft configuration paths.
/// </summary>
internal sealed class ConfigurationPathProjector
{
    /// <summary>
    /// Projects a logical path under a section path.
    /// </summary>
    /// <param name="sectionPath">The binding section path.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <returns>The Microsoft configuration path.</returns>
    public string Project(string sectionPath, LogicalPath logicalPath)
    {
        return Project(sectionPath, logicalPath, null);
    }

    /// <summary>
    /// Projects a logical path under a section path and resolves stable list item keys when needed.
    /// </summary>
    /// <param name="sectionPath">The binding section path.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <param name="listIndexResolver">Optional resolver from list parent path and item key to projected list index.</param>
    /// <returns>The Microsoft configuration path.</returns>
    public string Project(
        string sectionPath,
        LogicalPath logicalPath,
        Func<LogicalPath, string, int>? listIndexResolver)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sectionPath))
        {
            parts.Add(sectionPath);
        }

        for (var index = 0; index < logicalPath.Segments.Count; index++)
        {
            var segment = logicalPath.Segments[index];
            parts.Add(segment switch
            {
                ListItemKeySegment itemKey => ResolveListIndex(
                    logicalPath,
                    index,
                    itemKey,
                    listIndexResolver),
                _ => segment.Value
            });
        }

        return string.Join(':', parts);
    }

    private static string ResolveListIndex(
        LogicalPath logicalPath,
        int segmentIndex,
        ListItemKeySegment itemKey,
        Func<LogicalPath, string, int>? listIndexResolver)
    {
        if (listIndexResolver is null)
        {
            return itemKey.ItemKey;
        }

        var listPath = new LogicalPath(logicalPath.Segments.Take(segmentIndex).ToArray());
        return listIndexResolver(listPath, itemKey.ItemKey).ToString();
    }
}
