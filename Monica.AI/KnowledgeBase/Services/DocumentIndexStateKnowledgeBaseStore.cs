using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Services;

/// <summary>
/// Knowledge-base store backed by the existing document-index state store.
/// </summary>
public sealed class DocumentIndexStateKnowledgeBaseStore(
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
                KnowledgeBaseId = state.KnowledgeBaseId
            })
            .ToList();
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
