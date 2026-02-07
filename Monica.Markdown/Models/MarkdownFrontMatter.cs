namespace Monica.Markdown.Models;

/// <summary>
/// Parsed YAML front matter metadata from a markdown document.
/// </summary>
public class MarkdownFrontMatter
{
    /// <summary>
    /// Title extracted from front matter, if present.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Date extracted from front matter, if present.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Tags extracted from front matter.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// All raw key-value pairs from the YAML front matter.
    /// Uses case-insensitive keys for flexible metadata search.
    /// </summary>
    public Dictionary<string, object?> RawMetadata { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
