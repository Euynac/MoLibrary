using Monica.Markdown.UIMarkdown.Models;

namespace Monica.Markdown.Search;

internal interface IMarkdownDocumentSearchMatcher
{
    EMarkdownDocumentSearchAlgorithm Algorithm { get; }

    IReadOnlyList<MarkdownDocumentSearchCandidate> Search(
        MarkdownDocumentSearchGroupIndex groupIndex,
        string normalizedQuery,
        CancellationToken cancellationToken);
}
