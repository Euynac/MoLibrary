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

/// <summary>
/// Structured payload returned by the document browsing tool.
/// </summary>
internal sealed class KnowledgeDocumentBrowseToolPayload
{
    public string ToolName { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string? Query { get; init; }
    public string Message { get; init; } = string.Empty;
    public string NextStepInstruction { get; init; } = string.Empty;
    public int ResultCount { get; init; }
    public bool SearchContextAvailable { get; init; }
    public IReadOnlyList<KnowledgeSearchToolKnowledgeBase> KnowledgeBases { get; init; } = [];
    public IReadOnlyList<KnowledgeDocumentBrowseResultItem> Results { get; init; } = [];
}

/// <summary>
/// Structured payload returned by the document tree browsing tool.
/// </summary>
internal sealed class KnowledgeDocumentTreeToolPayload
{
    public string ToolName { get; init; } = string.Empty;
    public string? RequestedKnowledgeBaseId { get; init; }
    public string? DirectoryPath { get; init; }
    public int RequestedMaxDepth { get; init; }
    public int RequestedMaxEntries { get; init; }
    public string Message { get; init; } = string.Empty;
    public string NextStepInstruction { get; init; } = string.Empty;
    public bool DirectoryFound { get; init; }
    public bool HasMoreEntries { get; init; }
    public int DirectoryCount { get; init; }
    public int DocumentCount { get; init; }
    public IReadOnlyList<KnowledgeSearchToolKnowledgeBase> KnowledgeBases { get; init; } = [];
    public IReadOnlyList<KnowledgeDocumentTreeEntry> Entries { get; init; } = [];
}

/// <summary>
/// Directory or document entry returned by the tree browsing tool.
/// </summary>
internal sealed class KnowledgeDocumentTreeEntry
{
    public string KnowledgeBaseId { get; init; } = string.Empty;
    public string KnowledgeBaseName { get; init; } = string.Empty;
    public string EntryType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public int Depth { get; init; }
    public int? DocumentCount { get; init; }
    public string? DocumentId { get; init; }
    public string? SourceName { get; init; }
    public string? SourceLink { get; init; }
    public string? SourceKind { get; init; }
    public string? SourceGroupKey { get; init; }
    public string? Status { get; init; }
    public int? ChunkCount { get; init; }
    public DateTimeOffset? IndexedAt { get; init; }
}

/// <summary>
/// Document candidate returned by the browsing tool.
/// </summary>
internal sealed class KnowledgeDocumentBrowseResultItem
{
    public string KnowledgeBaseId { get; init; } = string.Empty;
    public string KnowledgeBaseName { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string SourceLink { get; init; } = string.Empty;
    public string SourceKind { get; init; } = string.Empty;
    public string? SourceGroupKey { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ChunkCount { get; init; }
    public DateTimeOffset? IndexedAt { get; init; }
    public double? Score { get; init; }
    public string? SectionTitle { get; init; }
    public string? PreviewText { get; init; }
    public string? AnchorId { get; init; }
    public string? MatchedText { get; init; }
    public string? PrefixContext { get; init; }
    public string? SuffixContext { get; init; }
}

/// <summary>
/// Structured payload returned by the document content tool.
/// </summary>
internal sealed class KnowledgeDocumentContentToolPayload
{
    public string ToolName { get; init; } = string.Empty;
    public string? RequestedDocumentId { get; init; }
    public string? RequestedSourceLink { get; init; }
    public string? RequestedKnowledgeBaseId { get; init; }
    public int RequestedMaxTokens { get; init; }
    public int? RawRequestedMaxTokens { get; init; }
    public int EffectiveMaxTokens { get; init; }
    public bool TokenBudgetClamped { get; init; }
    public int MaxSupportedTokens { get; init; }
    public int RequestedStartCharacterIndex { get; init; }
    public string Message { get; init; } = string.Empty;
    public string NextStepInstruction { get; init; } = string.Empty;
    public bool DuplicateRequestBlocked { get; init; }
    public bool ContentTruncated { get; init; }
    public bool CanContinue { get; init; }
    public int? SuggestedNextStartCharacterIndex { get; init; }
    public string TokenCountProviderId { get; init; } = string.Empty;
    public string TokenCountProviderName { get; init; } = string.Empty;
    public bool IsEstimatedTokenCount { get; init; }
    public int OriginalCharacterCount { get; init; }
    public int OriginalTokenCount { get; init; }
    public int ReturnedCharacterCount { get; init; }
    public int ReturnedTokenCount { get; init; }
    public int ReturnedStartCharacterIndex { get; init; }
    public int ReturnedEndCharacterIndex { get; init; }
    public int RemainingCharacterCount { get; init; }
    public int RemainingTokenCount { get; init; }
    public KnowledgeDocumentContentResultItem? Document { get; init; }
    public IReadOnlyList<KnowledgeDocumentBrowseResultItem> Candidates { get; init; } = [];
}

/// <summary>
/// Document payload returned when original document content is resolved successfully.
/// </summary>
internal sealed class KnowledgeDocumentContentResultItem
{
    public string KnowledgeBaseId { get; init; } = string.Empty;
    public string KnowledgeBaseName { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string SourceLink { get; init; } = string.Empty;
    public string SourceKind { get; init; } = string.Empty;
    public string? SourceGroupKey { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ChunkCount { get; init; }
    public DateTimeOffset? IndexedAt { get; init; }
    public string Content { get; init; } = string.Empty;
}
