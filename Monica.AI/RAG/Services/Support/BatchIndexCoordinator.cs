using Microsoft.Extensions.Logging;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Monica.Markdown.Abstractions;

namespace Monica.AI.RAG.Services.Support;

/// <summary>
/// Coordinates batch indexing flow for one knowledge base.
/// </summary>
internal sealed class BatchIndexCoordinator(
    RAGDocumentService documentService,
    RAGDocumentIndexingService indexingService,
    RAGVectorStoreService vectorStoreService,
    RAGBatchIndexOperationRegistry operationRegistry,
    IMarkdownDocumentCatalog markdownService,
    MarkdownDocumentResolver markdownDocumentResolver,
    ILogger<BatchIndexCoordinator> logger)
{
    public bool IsBatchIndexingActive(string kbId)
    {
        if (string.IsNullOrWhiteSpace(kbId))
        {
            return false;
        }

        return operationRegistry.IsActive(kbId);
    }

    public bool TryCancelBatchIndexing(string kbId, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(kbId))
        {
            errorMessage = "Knowledge base id is required.";
            return false;
        }

        if (!operationRegistry.TryGet(kbId, out var operation) || operation is null)
        {
            errorMessage = "No active indexing task found. Cancellation treated as no-op.";
            return true;
        }

        errorMessage = operation.RequestCancellation();
        return true;
    }

    public async Task<string> CancelBatchIndexingAsync(
        string kbId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kbId))
        {
            throw new ArgumentException("Knowledge base id is required.", nameof(kbId));
        }

        if (operationRegistry.TryGet(kbId, out var operation) && operation is not null)
        {
            return operation.RequestCancellation();
        }

        var recoveredCount = await documentService.ConvergeInactiveAsync(kbId, cancellationToken);
        return recoveredCount > 0
            ? $"No active indexing task found. Reset {recoveredCount} stale indexing document(s) to pending."
            : "No active indexing task found. Cancellation treated as no-op.";
    }

    public async Task<RAGBatchIndexExecutionResult> StartBatchIndexingAsync(
        string kbId,
        int maxConcurrency = 5,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? documentIds = null)
    {
        RAGBatchIndexOperation? operation = null;
        CancellationTokenSource? linkedCancellation = null;

        try
        {
            logger.LogInformation(
                "Starting batch indexing for KB '{KbId}' with max concurrency {MaxConcurrency}",
                kbId,
                maxConcurrency);

            if (!operationRegistry.TryStart(kbId, out var requestedOperation))
            {
                return RAGBatchIndexExecutionResult.Failed("Batch indexing is already running.");
            }

            operation = requestedOperation;
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                operation.CancellationToken);
            var effectiveToken = linkedCancellation.Token;

            var queue = await documentService.GetQueueAsync(kbId, effectiveToken);
            var selectedDocumentIds = documentIds?
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Select(static id => id.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var pendingDocs = queue
                .Where(d => d.Status is DocumentStatus.Pending or DocumentStatus.Error)
                .Where(d => selectedDocumentIds is null || selectedDocumentIds.Contains(d.Id))
                .ToList();
            if (pendingDocs.Count == 0)
            {
                requestedOperation.Complete(RAGBatchIndexOperationState.Failed);
                return RAGBatchIndexExecutionResult.Failed("No pending documents to index.");
            }

            await vectorStoreService.EnsureIndexingReadyAsync(kbId, effectiveToken);

            var normalizedConcurrency = Math.Clamp(maxConcurrency, 1, 20);
            using var semaphore = new SemaphoreSlim(normalizedConcurrency, normalizedConcurrency);

            var tasks = pendingDocs.Select(async doc =>
            {
                await semaphore.WaitAsync(effectiveToken);
                try
                {
                    await IndexQueuedDocumentAsync(kbId, doc, requestedOperation, progress, effectiveToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            effectiveToken.ThrowIfCancellationRequested();

            var finalQueue = await documentService.GetQueueAsync(kbId, CancellationToken.None);
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

                requestedOperation.Complete(RAGBatchIndexOperationState.Failed);
                return RAGBatchIndexExecutionResult.Failed(
                    $"Batch indexing completed with errors. Success {processedDocs.Count - failedDocs.Count}/{processedDocs.Count}, " +
                    $"failed {failedDocs.Count}. {details}{suffix}");
            }

            requestedOperation.Complete(RAGBatchIndexOperationState.Succeeded);
            return RAGBatchIndexExecutionResult.Success(
                $"Batch indexing completed for {processedDocs.Count} documents.");
        }
        catch (OperationCanceledException) when (
            linkedCancellation?.IsCancellationRequested == true ||
            cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Batch indexing cancelled for KB '{KbId}'", kbId);
            operation?.Complete(RAGBatchIndexOperationState.Cancelled);
            return RAGBatchIndexExecutionResult.CancelledResult("Request was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start batch indexing for KB '{KbId}'", kbId);
            operation?.Complete(RAGBatchIndexOperationState.Failed);
            return ex is InvalidOperationException or KeyNotFoundException
                ? RAGBatchIndexExecutionResult.Failed(ex.Message)
                : RAGBatchIndexExecutionResult.Failed($"Failed to start batch indexing: {ex.Message}");
        }
        finally
        {
            linkedCancellation?.Dispose();

            if (operation is not null)
            {
                operationRegistry.Complete(kbId, operation);
            }
        }
    }

    public async Task StartDocumentIndexingAsync(
        string kbId,
        string documentId,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBatchIndexingActive(kbId))
        {
            throw new InvalidOperationException("Batch indexing is already running.");
        }

        var queue = await documentService.GetQueueAsync(kbId, cancellationToken);
        var queueItem = queue.FirstOrDefault(item =>
            string.Equals(item.Id, documentId, StringComparison.OrdinalIgnoreCase));

        if (queueItem is null)
        {
            throw new KeyNotFoundException(
                $"Document '{documentId}' was not found in the queue for knowledge base '{kbId}'.");
        }

        if (queueItem.Status == DocumentStatus.Done)
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' is already indexed. Queue it for reindex first.");
        }

        if (queueItem.Status == DocumentStatus.Indexing)
        {
            throw new InvalidOperationException($"Document '{documentId}' is already indexing.");
        }

        await vectorStoreService.EnsureIndexingReadyAsync(kbId, cancellationToken);
        await IndexQueuedDocumentAsync(kbId, queueItem, null, progress, cancellationToken);
    }

    private async Task IndexQueuedDocumentAsync(
        string kbId,
        DocumentQueueItem queueItem,
        RAGBatchIndexOperation? operation,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var markdownDocument = await markdownDocumentResolver.FindByPathAsync(queueItem.Id, cancellationToken);
            string content;
            string documentTitle;
            string sourceKind;

            if (markdownDocument is not null)
            {
                content = await markdownService.GetDocumentContentAsync(markdownDocument);
                documentTitle = markdownDocument.Title;
                sourceKind = KnowledgeDocumentSourceKinds.Markdown;
            }
            else
            {
                content = await documentService.GetSourceContentAsync(kbId, queueItem.Id, cancellationToken)
                          ?? throw new FileNotFoundException(
                              $"Document '{queueItem.Id}' source content was not found.");
                documentTitle = queueItem.Name;
                sourceKind = KnowledgeDocumentSourceKinds.Uploaded;
            }

            var lastPersistedProgress = queueItem.Progress;
            async Task HandleProgressAsync(IndexingProgress p, CancellationToken callbackToken)
            {
                if (p.TotalChunks > 0)
                {
                    var progressValue = Math.Clamp((int)((p.ProcessedChunks / (double)p.TotalChunks) * 100), 0, 100);
                    queueItem.Progress = progressValue;
                    queueItem.ChunkCount = p.TotalChunks;

                    if (progressValue != lastPersistedProgress)
                    {
                        lastPersistedProgress = progressValue;
                        try
                        {
                            await indexingService.UpdateProgressAsync(
                                kbId,
                                queueItem.Id,
                                progressValue,
                                p.TotalChunks,
                                callbackToken);
                        }
                        catch (OperationCanceledException) when (callbackToken.IsCancellationRequested)
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
                    }
                }

                operation?.ReportProgress(p);
                progress?.Report(p);
            }

            await indexingService.IndexAsync(
                kbId,
                queueItem.Id,
                documentTitle,
                content,
                HandleProgressAsync,
                cancellationToken,
                sourceKind: sourceKind);

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

                try
                {
                    await indexingService.MarkPendingAsync(kbId, queueItem.Id, CancellationToken.None);
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
            await indexingService.MarkFailedAsync(kbId, queueItem.Id, ex.GetBaseException().Message, CancellationToken.None);
        }
    }

}

/// <summary>
/// Outcome for batch indexing execution.
/// </summary>
public sealed record RAGBatchIndexExecutionResult(bool Succeeded, bool Cancelled, string Message)
{
    public static RAGBatchIndexExecutionResult Success(string message) => new(true, false, message);

    public static RAGBatchIndexExecutionResult Failed(string message) => new(false, false, message);

    public static RAGBatchIndexExecutionResult CancelledResult(string message) => new(false, true, message);
}
