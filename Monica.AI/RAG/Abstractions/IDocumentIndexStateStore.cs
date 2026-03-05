using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Unified store for knowledge-base metadata and document index states.
/// </summary>
public interface IDocumentIndexStateStore
{
    Task<IReadOnlyList<KnowledgeBase>> GetKnowledgeBasesAsync(CancellationToken ct = default);

    Task<KnowledgeBase?> GetKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);

    Task UpsertKnowledgeBaseAsync(KnowledgeBase knowledgeBase, CancellationToken ct = default);

    Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentIndexState>> GetDocumentStatesAsync(
        string knowledgeBaseId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DocumentIndexState>> GetAllDocumentStatesAsync(CancellationToken ct = default);

    Task<DocumentIndexState?> GetDocumentStateAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default);

    Task UpsertDocumentStateAsync(DocumentIndexState state, CancellationToken ct = default);

    Task DeleteDocumentStateAsync(string knowledgeBaseId, string documentPath, CancellationToken ct = default);

    Task DeleteKnowledgeBaseDocumentStatesAsync(string knowledgeBaseId, CancellationToken ct = default);
}
