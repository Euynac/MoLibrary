using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Abstractions;

/// <summary>
/// Store for knowledge-base metadata and document inventory state.
/// </summary>
public interface IKnowledgeBaseStore
{
    /// <summary>
    /// Gets all knowledge bases.
    /// </summary>
    Task<IReadOnlyList<Models.KnowledgeBase>> GetKnowledgeBasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets one knowledge base by identifier.
    /// </summary>
    Task<Models.KnowledgeBase?> GetKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates a knowledge base.
    /// </summary>
    Task UpsertKnowledgeBaseAsync(Models.KnowledgeBase knowledgeBase, CancellationToken ct = default);

    /// <summary>
    /// Deletes a knowledge base and its document inventory.
    /// </summary>
    Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);

    /// <summary>
    /// Gets the recorded document inventory for a knowledge base.
    /// </summary>
    Task<IReadOnlyList<DocumentQueueItem>> GetDocumentInventoryAsync(
        string knowledgeBaseId,
        CancellationToken ct = default);

    /// <summary>
    /// Adds pending document inventory rows and skips existing rows.
    /// </summary>
    Task<KnowledgeBaseDocumentImportResult> AddPendingDocumentsAsync(
        string knowledgeBaseId,
        IEnumerable<DocumentIndexState> documents,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes one document from the knowledge-base inventory.
    /// </summary>
    Task DeleteDocumentAsync(string knowledgeBaseId, string documentId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all document inventory rows for a knowledge base.
    /// </summary>
    Task<int> DeleteDocumentsAsync(string knowledgeBaseId, CancellationToken ct = default);
}
