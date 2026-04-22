namespace Monica.Markdown.Models;

/// <summary>
/// Parsed YAML front matter metadata from a markdown document.
/// </summary>
public class MarkdownFrontMatter
{
    /// <summary>
    /// Document title extracted from the <c>title</c> metadata field, if present.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Optional navigation label extracted from <c>sidebar_label</c>.
    /// When present, the built-in document tree uses this label instead of
    /// the document title while preserving the title used by the reader.
    /// </summary>
    public string? SidebarLabel { get; set; }

    /// <summary>
    /// Optional navigation order extracted from <c>sidebar_position</c>,
    /// <c>position</c>, or <c>order</c>.
    /// Lower values appear earlier in generated document trees.
    /// </summary>
    public int? SidebarPosition { get; set; }

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
