using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;

namespace Monica.AI.RAG.Services.Support;

/// <summary>
/// Resolves markdown documents by relative path.
/// </summary>
internal sealed class MarkdownDocumentResolver(IMarkdownDocumentCatalog markdownService)
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
