using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Markdown.Interfaces;

namespace Monica.AI.UI.Services;

/// <summary>
/// Coordinates chunk view retrieval with markdown fallback.
/// </summary>
public sealed class RAGChunkViewCoordinator(
    RAGService ragService,
    IMoMarkdownService markdownService,
    RAGMarkdownDocumentResolver markdownDocumentResolver)
{
    public async Task<(string OriginalText, IReadOnlyList<ChunkHighlight> Chunks)> GetDocumentChunksAsync(
        string kbId,
        string documentId,
        CancellationToken ct = default)
    {
        var indexedView = await ragService.GetDocumentChunkViewAsync(kbId, documentId, ct);
        if (indexedView is not null)
        {
            return (indexedView.OriginalText, indexedView.Chunks);
        }

        var markdownDocument = await markdownDocumentResolver.FindByPathAsync(documentId, ct);
        if (markdownDocument is null)
        {
            throw new FileNotFoundException(
                $"Document '{documentId}' has no available source content for chunk preview.");
        }

        var originalText = await markdownService.GetDocumentContentAsync(markdownDocument);
        var previewView = await ragService.BuildDocumentChunkPreviewAsync(
            kbId,
            markdownDocument.RelativePath,
            markdownDocument.Title,
            originalText,
            ct);

        return (previewView.OriginalText, previewView.Chunks);
    }
}
