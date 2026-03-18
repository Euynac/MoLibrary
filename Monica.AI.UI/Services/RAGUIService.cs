using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Tool.MoResponse;

namespace Monica.AI.UI.Services;

/// <summary>
/// UI service for RAG operations. Uses Res&lt;T&gt; for Blazor consumption.
/// Wraps infrastructure services, catching exceptions and returning Res.
/// </summary>
public class RAGUIService(
    IServiceProvider serviceProvider,
    IMoMarkdownService markdownService,
    ILogger<RAGUIService> logger)
{
    public async Task<Res<IReadOnlyList<KnowledgeBase>>> GetKnowledgeBasesAsync()
    {
        try
        {
            var result = await GetRagService().GetKnowledgeBasesAsync();
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get knowledge bases");
            return Res.Fail($"Failed to load knowledge bases: {ex.Message}");
        }
    }

    public async Task<Res<KnowledgeBaseVectorValidationResult>> GetKnowledgeBaseVectorValidationAsync(string kbId)
    {
        try
        {
            var result = await GetRagService().ValidateKnowledgeBaseVectorsAsync(kbId);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate vectors for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to validate vectors: {ex.Message}");
        }
    }

    public async Task<Res<KnowledgeBase>> CreateKnowledgeBaseAsync(
        string id,
        string name,
        string? description = null)
    {
        try
        {
            var kb = await GetRagService().CreateKnowledgeBaseAsync(id, name, description);
            return kb;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create knowledge base '{KbId}' ('{Name}')", id, name);
            return Res.Fail($"Failed to create knowledge base: {ex.Message}");
        }
    }

    public async Task<Res<KnowledgeBase>> UpdateKnowledgeBaseAsync(
        string id,
        string name,
        string? description = null)
    {
        try
        {
            var kb = await GetRagService().UpdateKnowledgeBaseAsync(id, name, description);
            return kb;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update knowledge base '{Id}'", id);
            return Res.Fail($"Failed to update knowledge base: {ex.Message}");
        }
    }

    public async Task<Res> DeleteKnowledgeBaseAsync(string id)
    {
        try
        {
            await GetRagService().DeleteKnowledgeBaseAsync(id);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete knowledge base '{Id}'", id);
            return Res.Fail($"Failed to delete knowledge base: {ex.Message}");
        }
    }

    public async Task<Res<IReadOnlyList<TextSearchResult>>> SearchAsync(
        string query,
        IEnumerable<string> kbIds,
        int topK = 5,
        RAGSearchEmbeddingOverride? embeddingOverride = null)
    {
        try
        {
            var results = await GetRagService().SearchAsync(
                query,
                kbIds,
                topK,
                embeddingOverride);
            return Res.Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query '{Query}'", query);
            return Res.Fail($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Indexes all documents from a markdown group into a knowledge base.
    /// </summary>
    public async Task<Res> IndexMarkdownGroupAsync(
        string kbId,
        string groupKey,
        IProgress<IndexingProgress>? progress = null)
    {
        try
        {
            var documents = await markdownService.GetDocumentsAsync(groupKey);
            if (documents.Count == 0)
            {
                return Res.Fail("No documents found in the selected group.");
            }

            Func<IndexingProgress, CancellationToken, Task>? progressCallback = progress is null
                ? null
                : (indexingProgress, _) =>
                {
                    progress.Report(indexingProgress);
                    return Task.CompletedTask;
                };

            var indexed = 0;
            foreach (var doc in documents)
            {
                var content = await markdownService.GetDocumentContentAsync(doc);
                await GetRagService().IndexDocumentAsync(
                    kbId,
                    doc.RelativePath,
                    doc.Title,
                    content,
                    progressCallback,
                    sourceKind: KnowledgeDocumentSourceKinds.Markdown,
                    sourceGroupKey: groupKey);

                indexed++;
                progress?.Report(new IndexingProgress(indexed, documents.Count, doc.Title));
            }

            return Res.Ok($"Indexed {indexed} documents.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index markdown group '{GroupKey}' into KB '{KbId}'", groupKey, kbId);
            return Res.Fail($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Indexes one uploaded document into a knowledge base.
    /// </summary>
    public async Task<Res> UploadAndIndexDocumentAsync(string kbId, string fileName, string content)
    {
        try
        {
            await GetRagService().IndexDocumentAsync(
                kbId,
                fileName,
                fileName,
                content,
                sourceKind: KnowledgeDocumentSourceKinds.Uploaded);

            return Res.Ok($"Indexed '{fileName}' successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index uploaded document '{FileName}'", fileName);
            return Res.Fail($"Upload indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all markdown document groups.
    /// </summary>
    public async Task<Res<List<MarkdownDocumentGroup>>> GetMarkdownGroupsAsync()
    {
        try
        {
            var groups = await markdownService.GetAllDocumentGroupsAsync();
            return groups;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get markdown groups");
            return Res.Fail($"Failed to load markdown groups: {ex.Message}");
        }
    }

    #region Document Queue Management

    public async Task<Res<IReadOnlyList<DocumentQueueItem>>> GetDocumentQueueAsync(string kbId)
    {
        try
        {
            var queue = await GetRagService().GetDocumentQueueAsync(kbId);
            return Res.Ok(queue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get document queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to load document queue: {ex.Message}");
        }
    }

    public async Task<Res> AddDocumentsToQueueAsync(
        string kbId,
        IEnumerable<string> documentIds,
        string? sourceGroupKey = null)
    {
        try
        {
            var docIdList = documentIds.ToList();
            logger.LogInformation("Adding {Count} documents to queue for KB '{KbId}'", docIdList.Count, kbId);

            var addedCount = await GetRagService().AddDocumentsToQueueAsync(
                kbId,
                docIdList,
                KnowledgeDocumentSourceKinds.Markdown,
                sourceGroupKey);

            return Res.Ok($"Added {addedCount} documents to queue.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add documents to queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to add documents: {ex.Message}");
        }
    }

    public async Task<Res> RemoveDocumentAsync(string kbId, string documentId)
    {
        try
        {
            logger.LogInformation("Removing document '{DocumentId}' from KB '{KbId}'", documentId, kbId);
            await GetRagService().RemoveDocumentAsync(kbId, documentId);
            return Res.Ok("Document removed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove document '{DocumentId}' from KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to remove document: {ex.Message}");
        }
    }

    public async Task<Res<int>> ClearKnowledgeBaseDocumentsAsync(string kbId)
    {
        try
        {
            var removedCount = await GetRagService().ClearKnowledgeBaseDocumentsAsync(kbId);
            return Res.Ok(removedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear documents for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to clear documents: {ex.Message}");
        }
    }

    public async Task<Res<int>> ClearDocumentQueueAsync(string kbId)
    {
        try
        {
            var removedCount = await GetRagService().ClearDocumentQueueAsync(kbId);
            return Res.Ok(removedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to clear queue: {ex.Message}");
        }
    }

    public async Task<Res> ReindexDocumentAsync(
        string kbId,
        string documentId,
        IProgress<IndexingProgress>? progress = null)
    {
        try
        {
            logger.LogInformation("Reindexing document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            await GetRagService().QueueDocumentForReindexAsync(kbId, documentId);
            return Res.Ok("Document queued for reindexing.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reindex document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to reindex document: {ex.Message}");
        }
    }

    public async Task<Res<int>> ReindexKnowledgeBaseAsync(string kbId)
    {
        try
        {
            logger.LogInformation("Queueing all documents in KB '{KbId}' for reindex", kbId);
            var queuedCount = await GetRagService().QueueKnowledgeBaseForReindexAsync(kbId);
            return Res.Ok(queuedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue KB '{KbId}' for reindex", kbId);
            return Res.Fail($"Failed to reindex knowledge base: {ex.Message}");
        }
    }

    public async Task<Res> StartDocumentIndexingAsync(
        string kbId,
        string documentId,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetBatchIndexCoordinator().StartDocumentIndexingAsync(
                kbId,
                documentId,
                progress,
                cancellationToken);
            return Res.Ok("Document indexed successfully.");
        }
        catch (OperationCanceledException)
        {
            return Res.Fail("Request was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to start indexing for document '{DocumentId}' in KB '{KbId}'",
                documentId,
                kbId);
            return Res.Fail($"Failed to start document indexing: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts queue-based batch indexing.
    /// </summary>
    public Task<Res> StartBatchIndexingAsync(
        string kbId,
        int maxConcurrency = 5,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return StartBatchIndexingCoreAsync(kbId, maxConcurrency, progress, cancellationToken);
    }

    public Res CancelBatchIndexing(string kbId)
    {
        return GetBatchIndexCoordinator().TryCancelBatchIndexing(kbId, out var message)
            ? Res.Ok(string.IsNullOrWhiteSpace(message) ? "Cancellation requested." : message)
            : Res.Fail(message);
    }

    public async Task<Res> CancelBatchIndexingAsync(string kbId, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await GetBatchIndexCoordinator().CancelBatchIndexingAsync(kbId, cancellationToken);
            return Res.Ok(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel batch indexing for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to cancel batch indexing: {ex.Message}");
        }
    }

    public bool IsBatchIndexingActive(string kbId)
        => GetBatchIndexCoordinator().IsBatchIndexingActive(kbId);

    /// <summary>
    /// Gets available markdown documents from one group.
    /// </summary>
    public async Task<Res<IReadOnlyList<MarkdownDocument>>> GetAvailableDocumentsAsync(string groupKey)
    {
        try
        {
            var documents = await markdownService.GetDocumentsAsync(groupKey);
            return Res.Ok<IReadOnlyList<MarkdownDocument>>(documents);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get available documents from group '{GroupKey}'", groupKey);
            return Res.Fail($"Failed to load documents: {ex.Message}");
        }
    }

    #endregion

    #region Chunk Viewer

    /// <summary>
    /// Gets document original text and chunk highlights for the chunk viewer.
    /// </summary>
    public async Task<Res<DocumentChunkView>> GetDocumentChunksAsync(
        string kbId,
        string documentId)
    {
        try
        {
            logger.LogInformation("Getting chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);

            var view = await GetChunkViewCoordinator().GetDocumentChunksAsync(kbId, documentId);
            return Res.Ok(view);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to load document chunks: {ex.Message}");
        }
    }

    #endregion

    private async Task<Res> StartBatchIndexingCoreAsync(
        string kbId,
        int maxConcurrency,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var outcome = await GetBatchIndexCoordinator().StartBatchIndexingAsync(
            kbId,
            maxConcurrency,
            progress,
            cancellationToken);

        return outcome.Succeeded
            ? Res.Ok(outcome.Message)
            : Res.Fail(outcome.Message);
    }

    // Resolve heavy RAG graph only for methods that actually need it.
    private RAGService GetRagService()
        => serviceProvider.GetRequiredService<RAGService>();

    private RAGBatchIndexCoordinator GetBatchIndexCoordinator()
        => serviceProvider.GetRequiredService<RAGBatchIndexCoordinator>();

    private RAGChunkViewCoordinator GetChunkViewCoordinator()
        => serviceProvider.GetRequiredService<RAGChunkViewCoordinator>();
}
