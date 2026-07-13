using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Microsoft.Extensions.Logging;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Owns the RAG indexing queue, reindex transitions, and vector-aware document removal.
/// </summary>
internal sealed class RAGDocumentService(
    IDocumentIndexStateStore indexStateStore,
    IKnowledgeDocumentSourceStore sourceStore,
    RAGIndexStateCoordinator indexStates,
    RAGVectorCollectionCoordinator vectorCollections,
    RAGIndexingActivity indexingActivity,
    ILogger<RAGDocumentService> logger)
{
    /// <summary>Returns the queue after recovering stale indexing states.</summary>
    public async Task<IReadOnlyList<DocumentQueueItem>> GetQueueAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        _ = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        _ = await ConvergeInactiveAsync(knowledgeBaseId, ct);
        return await indexStates.GetDocumentQueueAsync(knowledgeBaseId, ct);
    }

    /// <summary>Recovers persisted indexing states that have no live process activity.</summary>
    public async Task<int> ConvergeInactiveAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var recovered = await indexStates.ConvergeInactiveIndexingDocumentsAsync(
            knowledgeBaseId,
            indexingActivity.IsActive,
            ct);
        if (recovered > 0)
        {
            logger.LogInformation(
                "Recovered {RecoveredCount} stale indexing documents in '{KnowledgeBaseId}'.",
                recovered,
                knowledgeBaseId);
        }

        return recovered;
    }

    /// <summary>Adds document paths to the pending indexing queue.</summary>
    public async Task<int> AddToQueueAsync(
        string knowledgeBaseId,
        IEnumerable<string> documentPaths,
        string sourceKind = KnowledgeDocumentSourceKinds.Markdown,
        string? sourceGroupKey = null,
        CancellationToken ct = default)
    {
        _ = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        return await indexStates.AddDocumentsToQueueAsync(
            knowledgeBaseId,
            documentPaths,
            sourceKind,
            sourceGroupKey,
            ct);
    }

    /// <summary>Returns persisted source content for one queued document.</summary>
    public async Task<string?> GetSourceContentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        _ = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        return await sourceStore.GetContentAsync(knowledgeBaseId, documentPath, ct);
    }

    /// <summary>Removes current vectors and returns a document to pending state.</summary>
    public async Task QueueForReindexAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStates.GetDocumentStateRequiredAsync(knowledgeBaseId, documentPath, ct);
        _ = await vectorCollections.RemoveIndexedDocumentDataAsync(knowledgeBase, state, ct);
        await indexStates.SetPendingStateAsync(state, resetChunkMetadata: true, ct);
        await indexStates.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);
    }

    /// <summary>Clears the vector collection and returns all documents to pending state.</summary>
    public async Task<int> QueueKnowledgeBaseForReindexAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        _ = await ConvergeInactiveAsync(knowledgeBaseId, ct);
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        EnsureNoActiveIndexing(states, "reindex all documents");
        await vectorCollections.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
        await indexStates.ResetKnowledgeBaseDocumentStatesForReindexAsync(knowledgeBaseId, ct);
        await indexStates.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);
        return states.Count;
    }

    /// <summary>Removes only non-indexed queue entries and their source content.</summary>
    public async Task<int> ClearPendingQueueAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        _ = await ConvergeInactiveAsync(knowledgeBaseId, ct);
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        EnsureNoActiveIndexing(states, "clear the queue");
        var queued = states.Where(static state => state.Status != DocumentStatus.Done).ToList();
        foreach (var state in queued)
        {
            await indexStates.DeleteDocumentStateAsync(knowledgeBaseId, state.DocumentPath, ct);
            await sourceStore.DeleteContentAsync(knowledgeBaseId, state.DocumentPath, ct);
        }

        await indexStates.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);
        return queued.Count;
    }

    private void EnsureNoActiveIndexing(IEnumerable<DocumentIndexState> states, string operation)
    {
        if (states.Any(state =>
                state.Status == DocumentStatus.Indexing
                && indexingActivity.IsActive(state.KnowledgeBaseId, state.DocumentPath)))
        {
            throw new InvalidOperationException($"Cannot {operation} while indexing is in progress.");
        }
    }
}
