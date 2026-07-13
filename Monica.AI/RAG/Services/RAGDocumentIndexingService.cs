using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Microsoft.Extensions.Logging;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Executes one document indexing operation and owns its runtime and persisted state transitions.
/// </summary>
internal sealed class RAGDocumentIndexingService(
    IKnowledgeDocumentSourceStore sourceStore,
    RAGEmbeddingBindingResolver embeddingBindings,
    RAGVectorCollectionCoordinator vectorCollections,
    RAGIndexStateCoordinator indexStates,
    RAGIndexingActivity indexingActivity,
    ChunkerRegistry chunkers,
    ILogger<RAGDocumentIndexingService> logger)
{
    private const int EMBEDDING_PROGRESS_BATCH_SIZE = 16;

    /// <summary>Indexes one document and persists its vector and source state atomically by phase.</summary>
    public async Task IndexAsync(
        string knowledgeBaseId,
        string documentPath,
        string documentTitle,
        string content,
        Func<IndexingProgress, CancellationToken, Task>? progressCallback = null,
        CancellationToken ct = default,
        string? sourceKind = null,
        string? sourceGroupKey = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Cannot index an empty document.");
        }

        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var existingState = await indexStates.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct);
        var documentName = RAGIndexStateCoordinator.ResolveDocumentName(documentTitle, documentPath);
        var resolvedSourceKind = RAGIndexStateCoordinator.NormalizeSourceKind(sourceKind ?? existingState?.SourceKind);
        var resolvedSourceGroupKey = sourceGroupKey ?? existingState?.SourceGroupKey;
        var activeState = existingState ?? new DocumentIndexState
        {
            KnowledgeBaseId = knowledgeBaseId,
            DocumentPath = documentPath,
            DocumentName = documentName
        };

        using var activity = indexingActivity.Begin(knowledgeBaseId, documentPath);
        try
        {
            await indexStates.SetIndexingStateAsync(
                activeState,
                documentName,
                resolvedSourceKind,
                resolvedSourceGroupKey,
                ct);
            var chunker = await chunkers.ResolveChunkerAsync(documentPath, ct);
            var chunks = chunker.ChunkDocument(content, documentPath, documentName);
            if (chunks.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Chunker '{chunker.ChunkerId}' produced no chunks for '{documentPath}'.");
            }

            var binding = await embeddingBindings.ResolveAsync(knowledgeBase, ct);
            var generator = embeddingBindings.GetEmbeddingGenerator(binding);
            var collection = await vectorCollections.GetOrCreateCollectionAsync(knowledgeBase, binding, ct);
            var vectors = new List<float[]>(chunks.Count);
            await ReportProgressAsync(progressCallback, new IndexingProgress(0, chunks.Count, documentName), ct);

            foreach (var batch in chunks.Chunk(EMBEDDING_PROGRESS_BATCH_SIZE))
            {
                ct.ThrowIfCancellationRequested();
                var texts = batch.Select(static chunk => chunk.Content).ToList();
                var generated = await generator.GenerateAsync(texts, cancellationToken: ct);
                var batchVectors = generated.Select(static embedding => embedding.Vector.ToArray()).ToList();
                if (batchVectors.Count != texts.Count)
                {
                    throw new InvalidOperationException(
                        $"Embedding generator returned {batchVectors.Count} vectors for {texts.Count} chunks.");
                }

                vectors.AddRange(batchVectors);
                await ReportProgressAsync(
                    progressCallback,
                    new IndexingProgress(vectors.Count, chunks.Count, documentName),
                    ct);
            }

            _ = await vectorCollections.RemoveIndexedDocumentDataAsync(knowledgeBase, existingState, ct);
            var records = chunks.Select((chunk, index) => RAGVectorRecord.Create(
                    knowledgeBaseId,
                    documentPath,
                    documentName,
                    chunk,
                    chunker.ChunkerId,
                    vectors[index]))
                .ToList();
            await collection.UpsertAsync(records, ct);
            await sourceStore.SaveContentAsync(knowledgeBaseId, documentPath, content, ct);
            await indexStates.SetDoneStateAsync(activeState, chunks.Count, chunker.ChunkerId, ct);
            await indexStates.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var translatedMessage = RAGFailureTranslator.DescribeDocumentIndexing(ex);
            await TryMarkFailedAsync(knowledgeBaseId, documentPath, translatedMessage, ct);
            if (!string.Equals(translatedMessage, ex.Message, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(translatedMessage, ex);
            }

            throw;
        }
    }

    /// <summary>Marks a cancelled indexing document pending.</summary>
    public async Task MarkPendingAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        _ = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStates.GetDocumentStateRequiredAsync(knowledgeBaseId, documentPath, ct);
        await indexStates.SetPendingStateAsync(state, resetChunkMetadata: false, ct);
    }

    /// <summary>Persists progress for a currently indexing document.</summary>
    public async Task UpdateProgressAsync(
        string knowledgeBaseId,
        string documentPath,
        int progress,
        int chunkCount,
        CancellationToken ct = default)
    {
        _ = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStates.GetDocumentStateRequiredAsync(knowledgeBaseId, documentPath, ct);
        await indexStates.SetIndexingProgressAsync(state, progress, chunkCount, ct);
    }

    /// <summary>Persists a terminal indexing error.</summary>
    public async Task MarkFailedAsync(
        string knowledgeBaseId,
        string documentPath,
        string errorMessage,
        CancellationToken ct = default)
    {
        _ = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        await indexStates.MarkDocumentFailedAsync(knowledgeBaseId, documentPath, errorMessage, ct);
    }

    private async Task TryMarkFailedAsync(
        string knowledgeBaseId,
        string documentPath,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            await indexStates.MarkDocumentFailedAsync(knowledgeBaseId, documentPath, errorMessage, ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to persist indexing error for '{DocumentPath}' in '{KnowledgeBaseId}'.",
                documentPath,
                knowledgeBaseId);
        }
    }

    private static Task ReportProgressAsync(
        Func<IndexingProgress, CancellationToken, Task>? callback,
        IndexingProgress progress,
        CancellationToken ct)
        => callback is null ? Task.CompletedTask : callback(progress, ct);
}
