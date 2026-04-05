using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Markdown.Abstractions;

namespace Monica.AI.RAG.Services.Support;

/// <summary>
/// Coordinates batch indexing flow for one knowledge base.
/// </summary>
public sealed class BatchIndexCoordinator(
    RAGService ragService,
    IMarkdownDocumentCatalog markdownService,
    MarkdownDocumentResolver markdownDocumentResolver,
    ILogger<BatchIndexCoordinator> logger)
{
    private static readonly ConcurrentDictionary<string, int> ActiveBatchCounters =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveBatchCancellationSources =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsBatchIndexingActive(string kbId)
    {
        if (string.IsNullOrWhiteSpace(kbId))
        {
            return false;
        }

        return ActiveBatchCounters.TryGetValue(kbId, out var count) && count > 0;
    }

    public bool TryCancelBatchIndexing(string kbId, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(kbId))
        {
            errorMessage = "Knowledge base id is required.";
            return false;
        }

        if (!ActiveBatchCancellationSources.TryGetValue(kbId, out var cts))
        {
            errorMessage = "No active indexing task found. Cancellation treated as no-op.";
            return true;
        }

        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
            errorMessage = "Cancellation requested.";
            return true;
        }

        errorMessage = "Cancellation already requested.";
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

        if (ActiveBatchCancellationSources.TryGetValue(kbId, out var cts))
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
                return "Cancellation requested.";
            }

            return "Cancellation already requested.";
        }

        var recoveredCount = await ragService.ConvergeInactiveIndexingDocumentsAsync(kbId, cancellationToken);
        return recoveredCount > 0
            ? $"No active indexing task found. Reset {recoveredCount} stale indexing document(s) to pending."
            : "No active indexing task found. Cancellation treated as no-op.";
    }

    public async Task<RAGBatchIndexExecutionResult> StartBatchIndexingAsync(
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
                kbId,
                maxConcurrency);

            var requestedCancellation = new CancellationTokenSource();
            if (!ActiveBatchCancellationSources.TryAdd(kbId, requestedCancellation))
            {
                requestedCancellation.Dispose();
                return RAGBatchIndexExecutionResult.Failed("Batch indexing is already running.");
            }

            batchCancellation = requestedCancellation;
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                batchCancellation.Token);
            var effectiveToken = linkedCancellation.Token;

            var queue = await ragService.GetDocumentQueueAsync(kbId, effectiveToken);
            var pendingDocs = queue.Where(d => d.Status == DocumentStatus.Pending).ToList();
            if (pendingDocs.Count == 0)
            {
                return RAGBatchIndexExecutionResult.Failed("No pending documents to index.");
            }

            await ragService.EnsureKnowledgeBaseIndexingReadyAsync(kbId, effectiveToken);

            MarkBatchIndexingStarted(kbId);
            markedActive = true;

            var normalizedConcurrency = Math.Clamp(maxConcurrency, 1, 20);
            using var semaphore = new SemaphoreSlim(normalizedConcurrency, normalizedConcurrency);

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

            var finalQueue = await ragService.GetDocumentQueueAsync(kbId, CancellationToken.None);
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

                return RAGBatchIndexExecutionResult.Failed(
                    $"Batch indexing completed with errors. Success {processedDocs.Count - failedDocs.Count}/{processedDocs.Count}, " +
                    $"failed {failedDocs.Count}. {details}{suffix}");
            }

            return RAGBatchIndexExecutionResult.Success(
                $"Batch indexing completed for {processedDocs.Count} documents.");
        }
        catch (OperationCanceledException) when (
            linkedCancellation?.IsCancellationRequested == true ||
            cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Batch indexing cancelled for KB '{KbId}'", kbId);
            return RAGBatchIndexExecutionResult.CancelledResult("Request was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start batch indexing for KB '{KbId}'", kbId);
            return ex is InvalidOperationException or KeyNotFoundException
                ? RAGBatchIndexExecutionResult.Failed(ex.Message)
                : RAGBatchIndexExecutionResult.Failed($"Failed to start batch indexing: {ex.Message}");
        }
        finally
        {
            linkedCancellation?.Dispose();

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

        var queue = await ragService.GetDocumentQueueAsync(kbId, cancellationToken);
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

        await ragService.EnsureKnowledgeBaseIndexingReadyAsync(kbId, cancellationToken);
        await IndexQueuedDocumentAsync(kbId, queueItem, progress, cancellationToken);
    }

    private async Task IndexQueuedDocumentAsync(
        string kbId,
        DocumentQueueItem queueItem,
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
                content = await ragService.GetDocumentSourceContentAsync(kbId, queueItem.Id, cancellationToken)
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
                            await ragService.UpdateDocumentIndexingProgressAsync(
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

                progress?.Report(p);
            }

            await ragService.IndexDocumentAsync(
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
                    await ragService.MarkDocumentPendingAsync(kbId, queueItem.Id, CancellationToken.None);
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
            await ragService.MarkDocumentFailedAsync(kbId, queueItem.Id, ex.GetBaseException().Message, CancellationToken.None);
        }
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

/// <summary>
/// Outcome for batch indexing execution.
/// </summary>
public sealed record RAGBatchIndexExecutionResult(bool Succeeded, bool Cancelled, string Message)
{
    public static RAGBatchIndexExecutionResult Success(string message) => new(true, false, message);

    public static RAGBatchIndexExecutionResult Failed(string message) => new(false, false, message);

    public static RAGBatchIndexExecutionResult CancelledResult(string message) => new(false, true, message);
}
