using Monica.Markdown.Models;

namespace Monica.Markdown.Services.Support;

internal sealed record MarkdownDocumentSearchCandidate(
    MarkdownDocumentSearchEntry Entry,
    MarkdownDocumentSearchSection Section,
    string SourceText,
    IReadOnlyList<MarkdownSearchMatchSegment> HighlightSegments,
    MarkdownSearchMatchSegment PrimarySegment,
    double Score);
