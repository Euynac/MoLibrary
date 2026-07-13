namespace Monica.AI.RAG.Models;

/// <summary>
/// Structured payload returned by the semantic knowledge search tool.
/// </summary>
internal sealed class KnowledgeSearchToolPayload
{
    public string ToolName { get; init; } = string.Empty;
    public string SearchQuery { get; init; } = string.Empty;
    public string? UserQuestion { get; init; }
    public string Message { get; init; } = string.Empty;
    public string NextStepInstruction { get; init; } = string.Empty;
    public string CitationInstruction { get; init; } = string.Empty;
    public int ResultCount { get; init; }
    public bool ShouldRetrySameQuery { get; init; }
    public bool CanRetryWithRefinedQuery { get; init; }
    public IReadOnlyList<KnowledgeSearchToolKnowledgeBase> KnowledgeBases { get; init; } = [];
    public IReadOnlyList<KnowledgeSearchToolResultItem> Results { get; init; } = [];
}

/// <summary>
/// Shared tool payload for calls that cannot run because no knowledge base is selected.
/// </summary>
internal sealed class KnowledgeToolNoSelectionPayload
{
    public string ToolName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string NextStepInstruction { get; init; } = string.Empty;
    public int ResultCount { get; init; }
}

/// <summary>
/// Knowledge base metadata included in the search tool output.
/// </summary>
internal sealed class KnowledgeSearchToolKnowledgeBase
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// Individual search hit returned by the knowledge search tool.
/// </summary>
internal sealed class KnowledgeSearchToolResultItem
{
    public string? KnowledgeBaseId { get; init; }
    public string? KnowledgeBaseName { get; init; }
    public string? SourceName { get; init; }
    public string? SourceLink { get; init; }
    public string? SectionPath { get; init; }
    public string? DocumentId { get; init; }
    public int? ChunkIndex { get; init; }
    public double? Score { get; init; }
    public string? ScoreKind { get; init; }
    public double? SimilarityScore { get; init; }
    public string Content { get; init; } = string.Empty;
}
