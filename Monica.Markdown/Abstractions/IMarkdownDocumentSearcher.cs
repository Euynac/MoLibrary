using Monica.Markdown.Models;

namespace Monica.Markdown.Abstractions;

/// <summary>
/// Searches markdown documents by projected visible text and returns UI-friendly location metadata.
/// </summary>
public interface IMarkdownDocumentSearcher
{
    /// <summary>
    /// Searches markdown documents matching the supplied request.
    /// </summary>
    Task<IReadOnlyList<MarkdownDocumentSearchResult>> SearchAsync(
        MarkdownDocumentSearchRequest request,
        CancellationToken cancellationToken = default);
}
