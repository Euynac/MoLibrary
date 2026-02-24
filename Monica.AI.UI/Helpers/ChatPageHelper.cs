using Monica.AI.Models;

namespace Monica.AI.UI.Helpers;

/// <summary>
/// Helper class for AI Chat page business logic.
/// Extracts reusable logic from page components.
/// </summary>
public static class ChatPageHelper
{
    /// <summary>
    /// Updates reasoning support based on current provider and model.
    /// </summary>
    public static bool GetReasoningSupport(
        IReadOnlyList<AIProviderInfo> providers,
        string? providerId,
        string? modelName)
    {
        var provider = providers.FirstOrDefault(p => p.ProviderId == providerId);
        var modelInfo = provider?.SupportedModels?
            .OfType<LLMModelInfo>()
            .FirstOrDefault(m => m.ModelName == modelName);

        return modelInfo?.SupportsReasoning ?? false;
    }

    /// <summary>
    /// Generates a session title from the first message.
    /// </summary>
    public static string GenerateSessionTitle(string message)
    {
        return message.Length > 50 ? message[..50] + "..." : message;
    }

    /// <summary>
    /// Gets knowledge base IDs for session configuration, or null if none selected.
    /// </summary>
    public static List<string>? GetKnowledgeBaseIds(List<string> selectedIds)
    {
        return selectedIds.Count > 0 ? selectedIds : null;
    }

    /// <summary>
    /// Finds provider by ID.
    /// </summary>
    public static AIProviderInfo? FindProvider(
        IReadOnlyList<AIProviderInfo> providers,
        string? providerId)
    {
        return providers.FirstOrDefault(p => p.ProviderId == providerId);
    }

    /// <summary>
    /// Validates if a model exists in the provider's supported models.
    /// </summary>
    public static bool IsModelValid(
        AIProviderInfo? provider,
        string? modelName)
    {
        if (provider == null || string.IsNullOrEmpty(modelName))
            return false;

        return provider.SupportedModels?.Any(m => m.ModelName == modelName) ?? false;
    }
}
