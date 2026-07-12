using Monica.AI.KnowledgeBase.Abstractions;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Releases RAG-owned vector resources before KnowledgeBase removes metadata or source documents.
/// </summary>
internal sealed class RAGKnowledgeBaseLifecycleHandler(
    IKnowledgeBaseStore knowledgeBaseStore,
    IDocumentIndexStateStore indexStateStore,
    RAGVectorCollectionCoordinator vectorCollections,
    RAGIndexingActivity indexingActivity) : IKnowledgeBaseLifecycleHandler
{
    /// <inheritdoc />
    public async Task OnKnowledgeBaseDeletingAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await EnsureNoActiveIndexingAsync(knowledgeBaseId, ct);
        await vectorCollections.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
    }

    /// <inheritdoc />
    public async Task OnDocumentRemovingAsync(
        string knowledgeBaseId,
        string documentId,
        CancellationToken ct = default)
    {
        if (indexingActivity.IsActive(knowledgeBaseId, documentId))
        {
            throw new InvalidOperationException(
                $"Cannot remove document '{documentId}' while it is indexing.");
        }

        var knowledgeBase = await knowledgeBaseStore.GetKnowledgeBaseAsync(knowledgeBaseId, ct);
        if (knowledgeBase is null)
        {
            return;
        }

        var state = await indexStateStore.GetDocumentStateAsync(knowledgeBaseId, documentId, ct);
        _ = await vectorCollections.RemoveIndexedDocumentDataAsync(knowledgeBase, state, ct);
    }

    /// <inheritdoc />
    public async Task OnDocumentsClearingAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await EnsureNoActiveIndexingAsync(knowledgeBaseId, ct);
        await vectorCollections.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
    }

    private async Task EnsureNoActiveIndexingAsync(string knowledgeBaseId, CancellationToken ct)
    {
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        if (states.Any(state => indexingActivity.IsActive(state.KnowledgeBaseId, state.DocumentPath)))
        {
            throw new InvalidOperationException(
                $"Cannot modify knowledge base '{knowledgeBaseId}' while document indexing is active.");
        }
    }
}
