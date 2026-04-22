namespace Monica.Markdown.Models;

/// <summary>
/// Metadata applied to a folder node in a generated markdown document tree.
/// Folder metadata is read from a dedicated markdown file such as
/// <c>_category_.md</c> whose content is expected to contain only YAML front matter.
/// </summary>
/// <param name="RelativePath">Folder path relative to the document group root, using '/' separators.</param>
/// <param name="DisplayName">Resolved folder label used by navigation trees.</param>
/// <param name="NavigationOrder">Optional explicit order for this folder among its siblings.</param>
/// <param name="FrontMatter">Raw parsed front matter that produced this metadata.</param>
public sealed record MarkdownFolderMetadata(
    string RelativePath,
    string DisplayName,
    int? NavigationOrder,
    MarkdownFrontMatter FrontMatter);
