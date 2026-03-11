using Monica.Markdown.UIMarkdown.Models;

namespace Monica.Markdown.Search;

internal sealed record MarkdownDocumentSearchCandidate(
    MarkdownDocumentSearchEntry Entry,
    MarkdownDocumentSearchSection Section,
    string SourceText,
    IReadOnlyList<MarkdownSearchMatchSegment> HighlightSegments,
    MarkdownSearchMatchSegment PrimarySegment,
    double Score);
