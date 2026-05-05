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
/// Query mode used when searching knowledge-base source documents.
/// </summary>
public enum KnowledgeDocumentSearchMode
{
    /// <summary>
    /// Token-based fuzzy matching across document metadata and available source content.
    /// </summary>
    Fuzzy,

    /// <summary>
    /// Regular expression matching across document metadata and available source content.
    /// </summary>
    Regex
}

/// <summary>
/// Field that produced a knowledge-document search match.
/// </summary>
public enum KnowledgeDocumentSearchMatchField
{
    /// <summary>
    /// Match came from the document display name.
    /// </summary>
    Name,

    /// <summary>
    /// Match came from the document identifier or path.
    /// </summary>
    DocumentId,

    /// <summary>
    /// Match came from the document directory path.
    /// </summary>
    DirectoryPath,

    /// <summary>
    /// Match came from the source document content.
    /// </summary>
    Content
}

/// <summary>
/// Search hit returned by lookup-only knowledge-base search.
/// </summary>
public sealed record KnowledgeDocumentSearchResult(
    string KnowledgeBaseId,
    string DocumentId,
    string Name,
    string DirectoryPath,
    string Status,
    int ChunkCount,
    DateTimeOffset? IndexedAt,
    KnowledgeDocumentSearchMode Mode,
    KnowledgeDocumentSearchMatchField MatchField,
    double Score,
    string? PreviewText,
    int? MatchStartCharacterIndex,
    int? MatchLength,
    bool ContentSearched,
    bool ContentAvailable);

/// <summary>
/// Result returned when documents are imported into a knowledge base as pending inventory records.
/// </summary>
/// <param name="AddedCount">Number of newly added document records.</param>
/// <param name="SkippedCount">Number of duplicate document records skipped.</param>
public sealed record KnowledgeBaseDocumentImportResult(int AddedCount, int SkippedCount);
