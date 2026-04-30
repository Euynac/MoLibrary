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
}
