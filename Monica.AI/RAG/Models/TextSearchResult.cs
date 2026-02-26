namespace Monica.AI.RAG.Models;

/// <summary>
/// A single retrieved text search result.
/// Aligned with Microsoft Agent Framework's TextSearchResult pattern.
/// </summary>
public sealed class TextSearchResult
{
    public string? SourceName { get; set; }
    public string? SourceLink { get; set; }
    public string Text { get; set; } = string.Empty;
    public double? Score { get; set; }
    public string? KnowledgeBaseId { get; set; }
    public string? SectionPath { get; set; }
    public object? RawRepresentation { get; set; }

    /// <summary>
    /// Document ID for this search result (used to open chunk viewer).
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Chunk index within the document (used to highlight matched chunk).
    /// </summary>
    public int? ChunkIndex { get; set; }
}
