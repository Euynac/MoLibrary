namespace Monica.AI.RAG.Models;

/// <summary>
/// Summary returned after RAG support is removed from a knowledge base.
/// </summary>
public sealed record KnowledgeBaseRagSupportRemovalResult
{
    /// <summary>
    /// Knowledge base identifier.
    /// </summary>
    public required string KnowledgeBaseId { get; init; }

    /// <summary>
    /// Number of indexed document records that were reset to pending.
    /// </summary>
    public int ResetDocumentCount { get; init; }

    /// <summary>
    /// Number of previously indexed documents whose vector data was cleared.
    /// </summary>
    public int ClearedIndexedDocumentCount { get; init; }

    /// <summary>
    /// Number of previously indexed chunks whose vector data was cleared.
    /// </summary>
    public int ClearedChunkCount { get; init; }

    /// <summary>
    /// Vector collection name associated with the knowledge base.
    /// </summary>
    public string? VectorCollectionName { get; init; }

    /// <summary>
    /// Whether the remote vector store cleanup completed successfully.
    /// </summary>
    public bool VectorStoreCleanupSucceeded { get; init; } = true;

    /// <summary>
    /// Whether local RAG metadata was removed without contacting the vector store.
    /// </summary>
    public bool WasForced { get; init; }

    /// <summary>
    /// Whether the remote vector collection may still contain stale records.
    /// </summary>
    public bool StaleVectorCollectionMayRemain { get; init; }

    /// <summary>
    /// Vector-store cleanup failure details when a non-forced cleanup attempt failed before local removal.
    /// </summary>
    public string? VectorStoreCleanupErrorMessage { get; init; }
}
