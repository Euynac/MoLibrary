using Monica.Markdown.UIMarkdown.Models;

namespace Monica.Markdown.Interfaces;

/// <summary>
/// Searches markdown documents by projected visible text and returns UI-friendly location metadata.
/// </summary>
public interface IMarkdownDocumentSearchService
{
    /// <summary>
    /// Searches markdown documents matching the supplied request.
    /// </summary>
    Task<IReadOnlyList<MarkdownDocumentSearchResult>> SearchAsync(
        MarkdownDocumentSearchRequest request,
        CancellationToken cancellationToken = default);
}
