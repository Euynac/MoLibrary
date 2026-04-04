using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;

namespace Monica.AI.UI.Services;

/// <summary>
/// Resolves markdown documents by relative path.
/// </summary>
public sealed class RAGMarkdownDocumentResolver(IMarkdownDocumentCatalog markdownService)
{
    public async Task<MarkdownDocument?> FindByPathAsync(string documentPath, CancellationToken ct = default)
    {
        var groups = await markdownService.GetAllDocumentGroupsAsync();
        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            var documents = await markdownService.GetDocumentsAsync(group.Key);
            var matched = documents.FirstOrDefault(doc =>
                string.Equals(doc.RelativePath, documentPath, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched;
            }
        }

        return null;
    }
}
