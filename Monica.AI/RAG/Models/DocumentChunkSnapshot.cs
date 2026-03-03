namespace Monica.AI.RAG.Models;

/// <summary>
/// Persisted chunk metadata for one indexed document.
/// </summary>
public sealed class DocumentChunkSnapshot
{
    public required string KnowledgeBaseId { get; init; }

    public required string DocumentPath { get; init; }

    public required string DocumentTitle { get; init; }

    public required string ChunkerId { get; init; }

    public required string OriginalText { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public List<DocumentChunkSnapshotItem> Chunks { get; init; } = [];
}

/// <summary>
/// One chunk item stored in a document snapshot.
/// </summary>
public sealed class DocumentChunkSnapshotItem
{
    public required int Index { get; init; }

    public required int Start { get; init; }

    public required int End { get; init; }

    public string? Section { get; init; }
}
