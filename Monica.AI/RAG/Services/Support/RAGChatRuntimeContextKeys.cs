using Monica.AI.RAG.Models;
using Monica.AI.Services.Support;

namespace Monica.AI.RAG.Services.Support;

/// <summary>
/// Runtime-context keys owned by the RAG module.
/// </summary>
public static class RAGChatRuntimeContextKeys
{
    /// <summary>
    /// Selected knowledge bases used by RAG skill scripts during the current chat invocation.
    /// </summary>
    public static readonly AIChatRuntimeContextKey<RAGKnowledgeSelection> KnowledgeSelection =
        new("rag.knowledge-selection");
}
