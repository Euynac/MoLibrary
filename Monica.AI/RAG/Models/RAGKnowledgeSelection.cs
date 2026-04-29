namespace Monica.AI.RAG.Models;

/// <summary>
/// Knowledge bases selected for RAG retrieval in the current chat session.
/// </summary>
/// <param name="KnowledgeBaseIds">Selected knowledge-base identifiers.</param>
public sealed record RAGKnowledgeSelection(IReadOnlyList<string> KnowledgeBaseIds);
