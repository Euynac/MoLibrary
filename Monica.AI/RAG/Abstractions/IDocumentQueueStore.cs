using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Store for managing document indexing queue.
/// </summary>
public interface IDocumentQueueStore
{
    /// <summary>
    /// Get all documents in the queue for a knowledge base.
    /// </summary>
    Task<IReadOnlyList<DocumentQueueItem>> GetQueueAsync(string knowledgeBaseId, CancellationToken ct = default);

    /// <summary>
    /// Add a document to the queue.
    /// </summary>
    Task AddAsync(DocumentQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Update a document's status in the queue.
    /// </summary>
    Task UpdateAsync(DocumentQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Remove a document from the queue.
    /// </summary>
    Task RemoveAsync(string knowledgeBaseId, string documentId, CancellationToken ct = default);

    /// <summary>
    /// Get a specific document from the queue.
    /// </summary>
    Task<DocumentQueueItem?> GetByIdAsync(string knowledgeBaseId, string documentId, CancellationToken ct = default);
}
