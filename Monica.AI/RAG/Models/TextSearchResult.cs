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

    /// <summary>
    /// Raw score returned by the active search path.
    /// For hybrid search this is the fused ranking score, not a true similarity percentage.
    /// </summary>
    public double? Score { get; set; }

    /// <summary>
    /// Describes how <see cref="Score"/> should be interpreted.
    /// </summary>
    public TextSearchScoreKind ScoreKind { get; set; } = TextSearchScoreKind.VectorSimilarity;

    /// <summary>
    /// Optional vector similarity score for display when the raw search score is not percentage-safe.
    /// </summary>
    public double? SimilarityScore { get; set; }

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
