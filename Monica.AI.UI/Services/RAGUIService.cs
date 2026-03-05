using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Tool.MoResponse;
using System.Collections.Concurrent;

namespace Monica.AI.UI.Services;

/// <summary>
/// UI service for RAG operations. Uses Res&lt;T&gt; for Blazor consumption.
/// Wraps the infrastructure RAGService, catching exceptions and returning Res.
/// </summary>
public class RAGUIService(
    RAGService ragService,
    IMoMarkdownService markdownService,
    IDocumentQueueStore documentQueueStore,
    ILogger<RAGUIService> logger)
{
    private static readonly ConcurrentDictionary<string, int> ActiveBatchCounters =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveBatchCancellationSources =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<Res<IReadOnlyList<KnowledgeBase>>> GetKnowledgeBasesAsync()
    {
        try
        {
            var result = await ragService.GetKnowledgeBasesAsync();
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get knowledge bases");
            return Res.Fail($"Failed to load knowledge bases: {ex.Message}");
        }
    }

    public async Task<Res<KnowledgeBase>> CreateKnowledgeBaseAsync(
        string name, string? description = null)
    {
        try
        {
            var kb = await ragService.CreateKnowledgeBaseAsync(name, description);
            return kb;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create knowledge base '{Name}'", name);
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
            var kb = await ragService.UpdateKnowledgeBaseAsync(id, name, description);
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
            await ragService.DeleteKnowledgeBaseAsync(id);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete knowledge base '{Id}'", id);
            return Res.Fail($"Failed to delete knowledge base: {ex.Message}");
        }
    }

    public async Task<Res<IReadOnlyList<TextSearchResult>>> SearchAsync(
        string query, IEnumerable<string> kbIds, int topK = 5)
    {
        try
        {
            var results = await ragService.SearchAsync(query, kbIds, topK);
            return Res.Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query '{Query}'", query);
            return Res.Fail($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Index all documents from a markdown group into a knowledge base.
    /// </summary>
    public async Task<Res> IndexMarkdownGroupAsync(
        string kbId, string groupKey,
        IProgress<IndexingProgress>? progress = null)
    {
        try
        {
            var documents = await markdownService.GetDocumentsAsync(groupKey);
            if (documents.Count == 0)
                return Res.Fail("No documents found in the selected group.");

            var indexed = 0;
            foreach (var doc in documents)
            {
                var content = await markdownService.GetDocumentContentAsync(doc);
                await ragService.IndexDocumentAsync(
                    kbId, doc.RelativePath, doc.Title, content, progress);
                indexed++;
                progress?.Report(new IndexingProgress(indexed, documents.Count, doc.Title));
            }

            return Res.Ok($"Indexed {indexed} documents.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index markdown group '{GroupKey}' into KB '{KbId}'",
                groupKey, kbId);
            return Res.Fail($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Index a single uploaded document into a knowledge base.
    /// </summary>
    public async Task<Res> UploadAndIndexDocumentAsync(
        string kbId, string fileName, string content)
    {
        try
        {
            await ragService.IndexDocumentAsync(kbId, fileName, fileName, content);
            return Res.Ok($"Indexed '{fileName}' successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index uploaded document '{FileName}'", fileName);
            return Res.Fail($"Upload indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all registered markdown document groups (for the indexing panel).
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

    /// <summary>
    /// Get all documents in a knowledge base with their status.
    /// </summary>
    public async Task<Res<IReadOnlyList<DocumentQueueItem>>> GetDocumentQueueAsync(string kbId)
    {
        try
        {
            var queue = await ragService.GetDocumentQueueAsync(kbId);
            return Res.Ok(queue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get document queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to load document queue: {ex.Message}");
        }
    }

    /// <summary>
    /// Add documents to the indexing queue.
    /// </summary>
    public async Task<Res> AddDocumentsToQueueAsync(string kbId, IEnumerable<string> documentIds)
    {
        try
        {
            var docIdList = documentIds.ToList();
            logger.LogInformation("Adding {Count} documents to queue for KB '{KbId}'", docIdList.Count, kbId);

            // Get the markdown group to fetch document details
            foreach (var docId in docIdList)
            {
                // Check if document already exists in queue
                var existing = await documentQueueStore.GetByIdAsync(kbId, docId);
                if (existing != null)
                {
                    logger.LogWarning("Document '{DocId}' already exists in queue for KB '{KbId}'", docId, kbId);
                    continue;
                }

                // Create queue item
                var queueItem = new DocumentQueueItem
                {
                    Id = docId,
                    Name = Path.GetFileName(docId),
                    KnowledgeBaseId = kbId,
                    Status = DocumentStatus.Pending,
                    ChunkCount = 0,
                    Progress = 0
                };

                await documentQueueStore.AddAsync(queueItem);
            }

            return Res.Ok($"Added {docIdList.Count} documents to queue.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add documents to queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to add documents: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove a document from the queue or delete an indexed document.
    /// </summary>
    public async Task<Res> RemoveDocumentAsync(string kbId, string documentId)
    {
        try
        {
            logger.LogInformation("Removing document '{DocumentId}' from KB '{KbId}'", documentId, kbId);
            await ragService.RemoveDocumentAsync(kbId, documentId);

            return Res.Ok("Document removed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove document '{DocumentId}' from KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to remove document: {ex.Message}");
        }
    }

    /// <summary>
    /// Re-index an existing document.
    /// </summary>
    public async Task<Res> ReindexDocumentAsync(
        string kbId, string documentId, IProgress<IndexingProgress>? progress = null)
    {
        try
        {
            logger.LogInformation("Reindexing document '{DocumentId}' in KB '{KbId}'", documentId, kbId);

            var queueItem = await documentQueueStore.GetByIdAsync(kbId, documentId);
            if (queueItem == null)
            {
                return Res.Fail("Document not found in queue.");
            }

            // Update status to pending
            queueItem.Status = DocumentStatus.Pending;
            queueItem.Progress = 0;
            queueItem.ErrorMessage = null;
            await documentQueueStore.UpdateAsync(queueItem);

            return Res.Ok("Document queued for reindexing.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reindex document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to reindex document: {ex.Message}");
        }
    }

    /// <summary>
    /// Start batch indexing with parallel control.
    /// </summary>
    public async Task<Res> StartBatchIndexingAsync(
        string kbId,
        int maxConcurrency = 5,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var markedActive = false;
        CancellationTokenSource? batchCancellation = null;
        CancellationTokenSource? linkedCancellation = null;

        try
        {
            logger.LogInformation(
                "Starting batch indexing for KB '{KbId}' with max concurrency {MaxConcurrency}",
                kbId, maxConcurrency);

            var requestedCancellation = new CancellationTokenSource();
            if (!ActiveBatchCancellationSources.TryAdd(kbId, requestedCancellation))
            {
                requestedCancellation.Dispose();
                return Res.Fail("Batch indexing is already running.");
            }

            batchCancellation = requestedCancellation;
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                batchCancellation.Token);
            var effectiveToken = linkedCancellation.Token;

            // Get pending documents from queue
            var queue = await documentQueueStore.GetQueueAsync(kbId, effectiveToken);
            var pendingDocs = queue.Where(d => d.Status == DocumentStatus.Pending).ToList();

            if (pendingDocs.Count == 0)
            {
                return Res.Fail("No pending documents to index.");
            }

            MarkBatchIndexingStarted(kbId);
            markedActive = true;

            // Process documents with limited concurrency
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = pendingDocs.Select(async doc =>
            {
                await semaphore.WaitAsync(effectiveToken);
                try
                {
                    await IndexQueuedDocumentAsync(kbId, doc, progress, effectiveToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            var finalQueue = await documentQueueStore.GetQueueAsync(kbId, CancellationToken.None);
            var pendingDocIds = pendingDocs
                .Select(doc => doc.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var processedDocs = finalQueue
                .Where(doc => pendingDocIds.Contains(doc.Id))
                .ToList();
            var failedDocs = processedDocs
                .Where(doc => doc.Status == DocumentStatus.Error)
                .ToList();

            if (failedDocs.Count > 0)
            {
                var details = string.Join("; ", failedDocs.Take(3).Select(doc =>
                    $"{doc.Name}: {doc.ErrorMessage ?? "Unknown error"}"));
                var suffix = failedDocs.Count > 3 ? " ..." : string.Empty;

                return Res.Fail(
                    $"Batch indexing completed with errors. Success {processedDocs.Count - failedDocs.Count}/{processedDocs.Count}, " +
                    $"failed {failedDocs.Count}. {details}{suffix}");
            }

            return Res.Ok($"Batch indexing completed for {processedDocs.Count} documents.");
        }
        catch (OperationCanceledException) when (
            linkedCancellation?.IsCancellationRequested == true ||
            cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Batch indexing cancelled for KB '{KbId}'", kbId);
            return Res.Fail("Request was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start batch indexing for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to start batch indexing: {ex.Message}");
        }
        finally
        {
            if (linkedCancellation is not null)
            {
                linkedCancellation.Dispose();
            }

            if (batchCancellation is not null)
            {
                if (ActiveBatchCancellationSources.TryRemove(kbId, out var removed))
                {
                    removed.Dispose();
                }
                else
                {
                    batchCancellation.Dispose();
                }
            }

            if (markedActive)
            {
                MarkBatchIndexingCompleted(kbId);
            }
        }
    }

    public Res CancelBatchIndexing(string kbId)
    {
        if (string.IsNullOrWhiteSpace(kbId))
        {
            return Res.Fail("Knowledge base id is required.");
        }

        if (!ActiveBatchCancellationSources.TryGetValue(kbId, out var cts))
        {
            return Res.Fail("No active indexing task to cancel.");
        }

        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
        }

        return Res.Ok("Cancellation requested.");
    }

    public bool IsBatchIndexingActive(string kbId)
    {
        if (string.IsNullOrWhiteSpace(kbId))
        {
            return false;
        }

        return ActiveBatchCounters.TryGetValue(kbId, out var count) && count > 0;
    }

    private async Task IndexQueuedDocumentAsync(
        string kbId,
        DocumentQueueItem queueItem,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Update status to indexing
            queueItem.Status = DocumentStatus.Indexing;
            queueItem.Progress = 0;
            await documentQueueStore.UpdateAsync(queueItem, cancellationToken);

            // Get document content from markdown service (document ID is relative path).
            var document = await FindMarkdownDocumentByPathAsync(queueItem.Id);

            if (document == null)
            {
                throw new FileNotFoundException($"Document '{queueItem.Id}' not found in any markdown group.");
            }

            var content = await markdownService.GetDocumentContentAsync(document);

            var lastPersistedProgress = queueItem.Progress;
            var progressWriteSync = new object();
            var progressWriteChain = Task.CompletedTask;

            void EnqueueProgressPersist(int progressValue, int totalChunks)
            {
                lock (progressWriteSync)
                {
                    progressWriteChain = progressWriteChain.ContinueWith(async _ =>
                    {
                        try
                        {
                            queueItem.Progress = progressValue;
                            queueItem.ChunkCount = totalChunks;
                            await documentQueueStore.UpdateAsync(queueItem, cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            // Ignore cancellation while the batch is stopping.
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(
                                ex,
                                "Failed to persist indexing progress for document '{DocId}' in KB '{KbId}'",
                                queueItem.Id,
                                kbId);
                        }
                    }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
                }
            }

            // Index the document
            var indexProgress = new Progress<IndexingProgress>(p =>
            {
                if (p.TotalChunks > 0)
                {
                    var progressValue = (int)((p.ProcessedChunks / (double)p.TotalChunks) * 100);
                    queueItem.Progress = progressValue;
                    queueItem.ChunkCount = p.TotalChunks;

                    if (progressValue != lastPersistedProgress)
                    {
                        lastPersistedProgress = progressValue;
                        EnqueueProgressPersist(progressValue, p.TotalChunks);
                    }
                }

                progress?.Report(p);
            });

            await ragService.IndexDocumentAsync(
                kbId,
                document.RelativePath,
                document.Title,
                content,
                indexProgress,
                cancellationToken);

            Task pendingProgressWrites;
            lock (progressWriteSync)
            {
                pendingProgressWrites = progressWriteChain;
            }

            await pendingProgressWrites;

            // Update status to done
            queueItem.Status = DocumentStatus.Done;
            queueItem.Progress = 100;
            queueItem.IndexedAt = DateTimeOffset.UtcNow;
            queueItem.ErrorMessage = null;
            await documentQueueStore.UpdateAsync(queueItem, CancellationToken.None);

            logger.LogInformation("Successfully indexed document '{DocId}' in KB '{KbId}'", queueItem.Id, kbId);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Indexing cancelled for document '{DocId}' in KB '{KbId}'",
                    queueItem.Id,
                    kbId);

                queueItem.Status = DocumentStatus.Pending;
                queueItem.Progress = 0;
                queueItem.ErrorMessage = null;
                try
                {
                    await documentQueueStore.UpdateAsync(queueItem, CancellationToken.None);
                }
                catch (Exception updateEx)
                {
                    logger.LogWarning(
                        updateEx,
                        "Failed to reset cancelled document '{DocId}' to pending in KB '{KbId}'",
                        queueItem.Id,
                        kbId);
                }
                return;
            }

            logger.LogError(ex, "Failed to index document '{DocId}' in KB '{KbId}'", queueItem.Id, kbId);

            queueItem.Status = DocumentStatus.Error;
            queueItem.ErrorMessage = ex.Message;
            await documentQueueStore.UpdateAsync(queueItem, CancellationToken.None);
        }
    }

    /// <summary>
    /// Get available documents from a markdown group that can be added to the queue.
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
    /// Get document original text and chunk highlights for the chunk viewer.
    /// </summary>
    public async Task<Res<(string OriginalText, IReadOnlyList<ChunkHighlight> Chunks)>> GetDocumentChunksAsync(
        string kbId,
        string documentId)
    {
        try
        {
            logger.LogInformation("Getting chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);

            var indexedView = await ragService.GetDocumentChunkViewAsync(kbId, documentId);
            if (indexedView is not null)
            {
                return Res.Ok((indexedView.OriginalText, indexedView.Chunks));
            }

            var markdownDocument = await FindMarkdownDocumentByPathAsync(documentId);
            if (markdownDocument is null)
            {
                return Res.Fail(
                    $"Document '{documentId}' has no indexed chunk snapshot and no markdown source was found.");
            }

            var originalText = await markdownService.GetDocumentContentAsync(markdownDocument);
            var previewView = await ragService.BuildDocumentChunkPreviewAsync(
                kbId,
                markdownDocument.RelativePath,
                markdownDocument.Title,
                originalText);

            return Res.Ok((previewView.OriginalText, previewView.Chunks));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to load document chunks: {ex.Message}");
        }
    }

    #endregion

    private async Task<MarkdownDocument?> FindMarkdownDocumentByPathAsync(string documentPath)
    {
        var groups = await markdownService.GetAllDocumentGroupsAsync();
        foreach (var group in groups)
        {
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

    private static void MarkBatchIndexingStarted(string kbId)
    {
        ActiveBatchCounters.AddOrUpdate(kbId, 1, static (_, count) => count + 1);
    }

    private static void MarkBatchIndexingCompleted(string kbId)
    {
        var updated = ActiveBatchCounters.AddOrUpdate(
            kbId,
            0,
            static (_, count) => count > 1 ? count - 1 : 0);

        if (updated == 0)
        {
            ActiveBatchCounters.TryRemove(kbId, out _);
        }
    }
}
