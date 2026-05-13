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
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sectionPath))
        {
            parts.Add(sectionPath);
        }

        foreach (var segment in logicalPath.Segments)
        {
            parts.Add(segment.Value);
        }

        return string.Join(':', parts);
    }
}
