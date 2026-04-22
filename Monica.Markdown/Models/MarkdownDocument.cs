namespace Monica.Markdown.Models;

/// <summary>
/// Represents a single markdown document with its metadata and file information.
/// </summary>
public class MarkdownDocument
{
    /// <summary>
    /// Key of the document group this document belongs to.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Resolved display title for this document.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Resolved label used when this document appears in navigation trees.
    /// This may differ from <see cref="Title"/> when front matter supplies a
    /// shorter <c>sidebar_label</c>.
    /// </summary>
    public required string NavigationTitle { get; init; }

    /// <summary>
    /// Optional order used by navigation tree generation.
    /// Lower values appear before higher values within the same folder.
    /// </summary>
    public int? NavigationOrder { get; init; }

    /// <summary>
    /// Absolute file path of this document.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Relative path from the group base directory, using '/' as separator.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Depth in the folder hierarchy (0 = root level of the group).
    /// </summary>
    public required int Level { get; init; }

    /// <summary>
    /// Parsed YAML front matter metadata, if available.
    /// </summary>
    public MarkdownFrontMatter? FrontMatter { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// Last modification time in UTC.
    /// </summary>
    public required DateTime LastModifiedUtc { get; init; }
}
