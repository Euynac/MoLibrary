namespace Monica.AI.RAG.Models;

/// <summary>
/// Non-destructive status check for the vector collection assigned to a knowledge base ID.
/// </summary>
public sealed record KnowledgeBaseVectorCollectionStatus
{
    /// <summary>
    /// Knowledge base identifier.
    /// </summary>
    public required string KnowledgeBaseId { get; init; }

    /// <summary>
    /// Vector collection name derived from the configured RAG prefix and knowledge base ID.
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// Whether the vector store was reachable and the collection existence check completed.
    /// </summary>
    public bool WasChecked { get; init; }

    /// <summary>
    /// Whether the collection exists when <see cref="WasChecked" /> is true.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// Error details when the vector store could not be checked.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
