namespace Monica.AI.KnowledgeBase.Abstractions;

/// <summary>
/// Participates in destructive knowledge-base operations so optional features can remove
/// their owned resources without introducing concrete dependencies into KnowledgeBase.
/// </summary>
public interface IKnowledgeBaseLifecycleHandler
{
    /// <summary>
    /// Removes resources owned by this feature before a knowledge base is deleted.
    /// </summary>
    Task OnKnowledgeBaseDeletingAsync(string knowledgeBaseId, CancellationToken ct = default);

    /// <summary>
    /// Removes resources owned by this feature before a document is removed.
    /// </summary>
    Task OnDocumentRemovingAsync(
        string knowledgeBaseId,
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Removes resources owned by this feature before all documents are cleared.
    /// </summary>
    Task OnDocumentsClearingAsync(string knowledgeBaseId, CancellationToken ct = default);
}
