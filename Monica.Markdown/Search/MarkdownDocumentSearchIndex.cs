using Monica.Markdown.Models;

namespace Monica.Markdown.Search;

internal sealed record MarkdownDocumentSearchSection(
    string Title,
    string NormalizedTitle,
    string HeadingTrailText,
    string NormalizedHeadingTrail,
    string NormalizedText,
    string? AnchorId,
    int? HeadingLevel,
    bool IsDocumentSection);

internal sealed record MarkdownDocumentSearchEntry(
    string GroupTitle,
    MarkdownDocument Document,
    string NormalizedDocumentTitle,
    string NormalizedRelativePath,
    string? DocumentPathTrail,
    string NormalizedDocumentPathTrail,
    IReadOnlyList<MarkdownDocumentSearchSection> Sections);

internal sealed record MarkdownDocumentSearchGroupIndex(
    string GroupKey,
    string GroupTitle,
    string Fingerprint,
    IReadOnlyList<MarkdownDocumentSearchEntry> Entries);
