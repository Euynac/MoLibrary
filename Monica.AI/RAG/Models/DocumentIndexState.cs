namespace Monica.AI.RAG.Models;

/// <summary>
/// Unified persisted index state for one document in a knowledge base.
/// </summary>
public sealed class DocumentIndexState
{
    public required string KnowledgeBaseId { get; init; }

    public required string DocumentPath { get; init; }

    public required string DocumentName { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public int ChunkCount { get; set; }

    public int Progress { get; set; }

    public DateTimeOffset? IndexedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public long RowVersion { get; set; }

    public string? ChunkerId { get; set; }

    public string? SourceKind { get; set; }

    public string? SourceGroupKey { get; set; }
}

/// <summary>
/// Well-known source kinds for document index states.
/// </summary>
public static class KnowledgeDocumentSourceKinds
{
    public const string Markdown = "markdown";
    public const string Uploaded = "uploaded";
    public const string Unknown = "unknown";
}
