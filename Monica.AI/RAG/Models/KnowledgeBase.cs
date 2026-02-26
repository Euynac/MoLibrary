namespace Monica.AI.RAG.Models;

/// <summary>
/// Represents a knowledge base - a named collection of indexed document chunks.
/// </summary>
public record KnowledgeBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int DocumentCount { get; set; }
    public int ChunkCount { get; set; }

    /// <summary>
    /// Embedding model ID used for this knowledge base.
    /// Once set (on first indexing), it cannot be changed to prevent vector dimension mismatches.
    /// </summary>
    public string? EmbeddingModelId { get; set; }

    /// <summary>
    /// Custom tool description for the LLM when this KB is used in chat.
    /// If null, a default description is generated from the KB name.
    /// </summary>
    public string? SearchToolDescription { get; set; }
}
