namespace Monica.AI.KnowledgeBase.Models;

/// <summary>
/// Represents a knowledge base - a named collection of indexed document chunks.
/// </summary>
public record KnowledgeBase
{
    /// <summary>
    /// Immutable business ID used by the vector store, state store, and source storage.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name shown in the UI.
    /// </summary>
    public required string Name { get; init; }

    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int DocumentCount { get; set; }
    public int ChunkCount { get; set; }

    /// <summary>
    /// Embedding provider ID used for this knowledge base.
    /// </summary>
    public string? EmbeddingProviderId { get; set; }

    /// <summary>
    /// Embedding model name used for this knowledge base.
    /// </summary>
    public string? EmbeddingModelName { get; set; }

    /// <summary>
    /// Custom tool description for the LLM when this KB is used in chat.
    /// If null, a default description is generated from the KB name.
    /// </summary>
    public string? SearchToolDescription { get; set; }
}
