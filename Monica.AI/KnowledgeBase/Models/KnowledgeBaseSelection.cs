namespace Monica.AI.KnowledgeBase.Models;

/// <summary>
/// Per-invocation knowledge-base selection shared by lookup and RAG skills.
/// </summary>
/// <param name="KnowledgeBaseIds">Selected knowledge-base identifiers.</param>
public sealed record KnowledgeBaseSelection(IReadOnlyList<string> KnowledgeBaseIds);

