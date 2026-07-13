using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Providers;

/// <summary>
/// Knowledge-base store backed by the existing document-index state store.
/// </summary>
internal sealed class DocumentIndexStateKnowledgeBaseStore(
    IDocumentIndexStateStore stateStore) : IKnowledgeBaseStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Models.KnowledgeBase>> GetKnowledgeBasesAsync(CancellationToken ct = default)
        => stateStore.GetKnowledgeBasesAsync(ct);

    /// <inheritdoc />
    public Task<Models.KnowledgeBase?> GetKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
        => stateStore.GetKnowledgeBaseAsync(knowledgeBaseId, ct);

    /// <inheritdoc />
    public Task UpsertKnowledgeBaseAsync(Models.KnowledgeBase knowledgeBase, CancellationToken ct = default)
        => stateStore.UpsertKnowledgeBaseAsync(knowledgeBase, ct);

    /// <inheritdoc />
    public Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
        => stateStore.DeleteKnowledgeBaseAsync(knowledgeBaseId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentQueueItem>> GetDocumentInventoryAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        var states = await stateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        return states
            .OrderBy(state => state.DocumentName, StringComparer.OrdinalIgnoreCase)
            .Select(state => new DocumentQueueItem
            {
                Id = state.DocumentPath,
                Name = state.DocumentName,
                Status = state.Status,
                ChunkCount = state.ChunkCount,
                Progress = state.Progress,
                IndexedAt = state.IndexedAt,
                ErrorMessage = state.ErrorMessage,
                KnowledgeBaseId = state.KnowledgeBaseId,
                SourceKind = state.SourceKind,
                SourceGroupKey = state.SourceGroupKey
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<KnowledgeBaseDocumentImportResult> AddPendingDocumentsAsync(
        string knowledgeBaseId,
        IEnumerable<DocumentIndexState> documents,
        CancellationToken ct = default)
    {
        var addedCount = 0;
        var skippedCount = 0;

        foreach (var document in documents)
        {
            var existing = await stateStore.GetDocumentStateAsync(knowledgeBaseId, document.DocumentPath, ct);
            if (existing is not null)
            {
                skippedCount++;
                continue;
            }

            await stateStore.UpsertDocumentStateAsync(document, ct);
            addedCount++;
        }

        return new KnowledgeBaseDocumentImportResult(addedCount, skippedCount);
    }

    /// <inheritdoc />
    public Task DeleteDocumentAsync(string knowledgeBaseId, string documentId, CancellationToken ct = default)
        => stateStore.DeleteDocumentStateAsync(knowledgeBaseId, documentId, ct);

    /// <inheritdoc />
    public async Task<int> DeleteDocumentsAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var states = await stateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        await stateStore.DeleteKnowledgeBaseDocumentStatesAsync(knowledgeBaseId, ct);
        return states.Count;
    }
}
