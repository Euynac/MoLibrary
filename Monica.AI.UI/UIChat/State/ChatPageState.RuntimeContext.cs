using Monica.AI.KnowledgeBase.Models;
using Monica.AI.KnowledgeBase.Services.Support;
using Monica.AI.Services.Support;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private static AIChatRuntimeContext BuildRuntimeContext(IReadOnlyList<string> selectedKnowledgeBaseIds)
    {
        var knowledgeBaseIds = selectedKnowledgeBaseIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var context = AIChatRuntimeContext.Empty;

        if (knowledgeBaseIds is { Count: > 0 })
        {
            context = context.Set(
                KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection,
                new KnowledgeBaseSelection(knowledgeBaseIds));
        }

        return context;
    }
}
