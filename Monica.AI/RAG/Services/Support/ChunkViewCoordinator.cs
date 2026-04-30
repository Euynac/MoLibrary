using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Monica.Markdown.Abstractions;

namespace Monica.AI.RAG.Services.Support;

/// <summary>
/// Coordinates chunk view retrieval with markdown fallback.
/// </summary>
public sealed class ChunkViewCoordinator(
    RAGService ragService,
    IMarkdownDocumentCatalog markdownService,
    MarkdownDocumentResolver markdownDocumentResolver)
{
    public async Task<DocumentChunkView> GetDocumentChunksAsync(
        string kbId,
        string documentId,
        CancellationToken ct = default)
    {
        var indexedView = await ragService.GetDocumentChunkViewAsync(kbId, documentId, ct);
        if (indexedView is not null)
        {
            return await EnrichMarkdownMetadataAsync(indexedView, ct);
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
            sourceKind: KnowledgeDocumentSourceKinds.Markdown,
            sourceGroupKey: markdownDocument.GroupKey,
            ct: ct);

        return previewView;
    }

    private async Task<DocumentChunkView> EnrichMarkdownMetadataAsync(
        DocumentChunkView view,
        CancellationToken ct)
    {
        var shouldResolveMarkdownMetadata =
            string.Equals(view.SourceKind, KnowledgeDocumentSourceKinds.Markdown, StringComparison.OrdinalIgnoreCase)
            || string.Equals(view.SourceKind, KnowledgeDocumentSourceKinds.Unknown, StringComparison.OrdinalIgnoreCase);

        if (!shouldResolveMarkdownMetadata || !string.IsNullOrWhiteSpace(view.SourceGroupKey))
        {
            return view;
        }

        var markdownDocument = await markdownDocumentResolver.FindByPathAsync(view.DocumentPath, ct);
        if (markdownDocument is null)
        {
            return view;
        }

        return new DocumentChunkView
        {
            DocumentPath = markdownDocument.RelativePath,
            DocumentName = view.DocumentName,
            OriginalText = view.OriginalText,
            Chunks = view.Chunks,
            ChunkerId = view.ChunkerId,
            IsPreview = view.IsPreview,
            SourceKind = KnowledgeDocumentSourceKinds.Markdown,
            SourceGroupKey = markdownDocument.GroupKey
        };
    }
}
