namespace Monica.AI.RAG.Models;

/// <summary>
/// Represents a document in the indexing queue with its current status.
/// </summary>
public sealed class DocumentQueueItem
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Document file name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current status of the document.
    /// </summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    /// <summary>
    /// Number of chunks in this document (0 if not yet indexed).
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Indexing progress percentage (0-100) when Status is Indexing.
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Date when the document was indexed (null if not yet indexed).
    /// </summary>
    public DateTimeOffset? IndexedAt { get; set; }

    /// <summary>
    /// Error message if Status is Error.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Knowledge base ID this document belongs to.
    /// </summary>
    public required string KnowledgeBaseId { get; init; }
}
