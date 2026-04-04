namespace Monica.Markdown.Models;

/// <summary>
/// Represents the best search hit for a document returned to the markdown viewer dialog.
/// </summary>
public sealed record MarkdownDocumentSearchResult(
    string GroupKey,
    string GroupTitle,
    string DocumentRelativePath,
    string DocumentTitle,
    string? DocumentPathTrail,
    string? SectionTitle,
    string PreviewText,
    IReadOnlyList<MarkdownSearchMatchSegment> PreviewHighlights,
    string? AnchorId,
    MarkdownSearchLocator Locator,
    double Score);
