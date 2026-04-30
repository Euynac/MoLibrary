namespace Monica.AI.RAG.Models;

/// <summary>
/// Result returned after an existing vector collection is cleared for reuse by a knowledge base.
/// </summary>
public sealed record KnowledgeBaseVectorCollectionOverwriteResult
{
    /// <summary>
    /// Knowledge base identifier.
    /// </summary>
    public required string KnowledgeBaseId { get; init; }

    /// <summary>
    /// Cleared vector collection name.
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// Number of indexed document records reset to pending.
    /// </summary>
    public int ResetDocumentCount { get; init; }

    /// <summary>
    /// Number of previously indexed chunks represented by reset metadata.
    /// </summary>
    public int ClearedChunkCount { get; init; }
}
