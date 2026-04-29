using DocumentIndexStateModel = Monica.AI.KnowledgeBase.Models.DocumentIndexState;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.KnowledgeBase.Abstractions;

/// <summary>
/// Unified store for knowledge-base metadata and document index states.
/// </summary>
public interface IDocumentIndexStateStore
{
    Task<IReadOnlyList<KnowledgeBaseModel>> GetKnowledgeBasesAsync(CancellationToken ct = default);

    Task<KnowledgeBaseModel?> GetKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);

    Task UpsertKnowledgeBaseAsync(KnowledgeBaseModel knowledgeBase, CancellationToken ct = default);

    Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentIndexStateModel>> GetDocumentStatesAsync(
        string knowledgeBaseId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DocumentIndexStateModel>> GetAllDocumentStatesAsync(CancellationToken ct = default);

    Task<DocumentIndexStateModel?> GetDocumentStateAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default);

    Task UpsertDocumentStateAsync(DocumentIndexStateModel state, CancellationToken ct = default);

    Task DeleteDocumentStateAsync(string knowledgeBaseId, string documentPath, CancellationToken ct = default);

    Task DeleteKnowledgeBaseDocumentStatesAsync(string knowledgeBaseId, CancellationToken ct = default);
}
