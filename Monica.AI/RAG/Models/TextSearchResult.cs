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
}
