using Monica.AI.KnowledgeBase.Models;
using Monica.AI.KnowledgeBase.Services.Support;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
using Monica.AI.Services.Support;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private static AIChatRuntimeContext BuildRuntimeContext(
        IReadOnlyList<string> selectedKnowledgeBaseIds,
        IReadOnlyList<AgentCapabilityReference>? capabilityReferences = null)
    {
        var knowledgeBaseIds = selectedKnowledgeBaseIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var references = (capabilityReferences ?? [])
            .Where(static reference => !string.IsNullOrWhiteSpace(reference.Name))
            .DistinctBy(static reference => reference.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var context = AIChatRuntimeContext.Empty;

        if (knowledgeBaseIds is { Count: > 0 })
        {
            context = context.Set(
                KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection,
                new KnowledgeBaseSelection(knowledgeBaseIds));
        }

        if (references.Count > 0)
        {
            context = context.Set(AgentCapabilityChatRuntimeContextKeys.References, references);
        }

        return context;
    }
}
