using Monica.AI.KnowledgeBase.Models;
using Monica.AI.Services.Support;

namespace Monica.AI.KnowledgeBase.Models;

/// <summary>
/// Runtime-context keys owned by the knowledge-base module.
/// </summary>
public static class KnowledgeBaseChatRuntimeContextKeys
{
    /// <summary>
    /// Selected knowledge bases used by knowledge lookup and RAG scripts during the current chat invocation.
    /// </summary>
    public static readonly AIChatRuntimeContextKey<KnowledgeBaseSelection> KnowledgeSelection =
        new("knowledge-base.selection");
}
