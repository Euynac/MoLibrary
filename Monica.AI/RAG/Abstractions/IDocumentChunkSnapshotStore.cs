using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Persists document text and chunk metadata snapshots for chunk viewer rendering.
/// </summary>
public interface IDocumentChunkSnapshotStore
{
    Task<IReadOnlyList<DocumentChunkSnapshot>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DocumentChunkSnapshot>> GetByKnowledgeBaseAsync(
        string knowledgeBaseId,
        CancellationToken ct = default);

    Task<DocumentChunkSnapshot?> GetAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default);

    Task SaveAsync(DocumentChunkSnapshot snapshot, CancellationToken ct = default);

    Task RemoveAsync(string knowledgeBaseId, string documentPath, CancellationToken ct = default);

    Task RemoveKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);
}
