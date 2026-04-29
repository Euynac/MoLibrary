namespace Monica.AI.KnowledgeBase.Models;

/// <summary>
/// Summary information for a knowledge base.
/// </summary>
public sealed record KnowledgeBaseSummary(
    string Id,
    string Name,
    string? Description,
    int DocumentCount,
    int ChunkCount,
    bool IsRagEnabled);

/// <summary>
/// Summary information for one document in a knowledge base.
/// </summary>
public sealed record KnowledgeDocumentSummary(
    string KnowledgeBaseId,
    string DocumentId,
    string Name,
    string DirectoryPath,
    string Status,
    int ChunkCount,
    DateTimeOffset? IndexedAt);

/// <summary>
/// Directory tree node for knowledge-base documents.
/// </summary>
public sealed class KnowledgeDocumentTreeNode
{
    /// <summary>
    /// Node name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path represented by the node.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether the node is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Document id when this node represents a document.
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// Child nodes.
    /// </summary>
    public List<KnowledgeDocumentTreeNode> Children { get; init; } = [];
}

/// <summary>
/// Source content segment returned by a lookup-only knowledge-base read.
/// </summary>
public sealed record KnowledgeDocumentContent(
    string KnowledgeBaseId,
    string DocumentId,
    string Name,
    string Content,
    int StartCharacterIndex,
    int ReturnedCharacterCount,
    int TotalCharacterCount,
    bool HasMore,
    int? SuggestedNextStartCharacterIndex);

/// <summary>
/// Result returned when documents are imported into a knowledge base as pending inventory records.
/// </summary>
/// <param name="AddedCount">Number of newly added document records.</param>
/// <param name="SkippedCount">Number of duplicate document records skipped.</param>
public sealed record KnowledgeBaseDocumentImportResult(int AddedCount, int SkippedCount);
