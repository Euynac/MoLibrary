using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Markdown.Interfaces;

namespace Monica.AI.UI.Services;

/// <summary>
/// Coordinates batch indexing flow for one knowledge base.
/// </summary>
public sealed class RAGBatchIndexCoordinator(
    RAGService ragService,
    IMoMarkdownService markdownService,
    RAGMarkdownDocumentResolver markdownDocumentResolver,
    ILogger<RAGBatchIndexCoordinator> logger)
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
            errorMessage = "No active indexing task to cancel.";
            return false;
        }

        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
        }

        return true;
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
            return RAGBatchIndexExecutionResult.Failed($"Failed to start batch indexing: {ex.Message}");
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

    private async Task IndexQueuedDocumentAsync(
        string kbId,
        DocumentQueueItem queueItem,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await ragService.MarkDocumentIndexingAsync(kbId, queueItem.Id, cancellationToken);

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
                            await ragService.UpdateDocumentIndexingProgressAsync(
                                kbId,
                                queueItem.Id,
                                progressValue,
                                totalChunks,
                                cancellationToken);
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
                queueItem.Id,
                documentTitle,
                content,
                indexProgress,
                cancellationToken,
                sourceKind: sourceKind);

            Task pendingProgressWrites;
            lock (progressWriteSync)
            {
                pendingProgressWrites = progressWriteChain;
            }

            await pendingProgressWrites;

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
            await ragService.MarkDocumentFailedAsync(kbId, queueItem.Id, ex.Message, CancellationToken.None);
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
