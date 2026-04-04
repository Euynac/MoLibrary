using Monica.Markdown.Models;

namespace Monica.Markdown.Services.Support;

internal interface IMarkdownDocumentSearchMatcher
{
    MarkdownSearchAlgorithm Algorithm { get; }

    IReadOnlyList<MarkdownDocumentSearchCandidate> Search(
        MarkdownDocumentSearchGroupIndex groupIndex,
        string normalizedQuery,
        CancellationToken cancellationToken);
}
