using Monica.Configuration.Utils;

namespace Monica.Configuration.Models;

/// <summary>
/// Structured logical identity of a configuration node or value.
/// </summary>
/// <param name="Segments">The ordered path segments.</param>
public sealed record LogicalPath(IReadOnlyList<ConfigurationPathSegment> Segments)
{
    /// <summary>
    /// Gets the empty root logical path.
    /// </summary>
    public static LogicalPath Root { get; } = new([]);

    /// <summary>
    /// Gets the number of path segments.
    /// </summary>
    public int Depth => Segments.Count;

    /// <summary>
    /// Creates a logical path from property segments.
    /// </summary>
    /// <param name="properties">The property segment names.</param>
    /// <returns>A structured logical path.</returns>
    public static LogicalPath FromProperties(params string[] properties)
    {
        return new LogicalPath(properties.Select(x => (ConfigurationPathSegment)new PropertySegment(x)).ToArray());
    }

    /// <summary>
    /// Returns a new path with one appended segment.
    /// </summary>
    /// <param name="segment">The segment to append.</param>
    /// <returns>The new logical path.</returns>
    public LogicalPath Append(ConfigurationPathSegment segment)
    {
        return new LogicalPath([..Segments, segment]);
    }

    /// <summary>
    /// Converts this path to the canonical storage representation.
    /// </summary>
    /// <returns>The canonical string.</returns>
    public string ToCanonicalString()
    {
        return ConfigurationPathFormatter.Format(this);
    }

    /// <summary>
    /// Parses a canonical string into a structured logical path.
    /// </summary>
    /// <param name="canonical">The canonical string.</param>
    /// <returns>The parsed logical path.</returns>
    public static LogicalPath Parse(string canonical)
    {
        return ConfigurationPathFormatter.Parse(canonical);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToCanonicalString();
    }
}
