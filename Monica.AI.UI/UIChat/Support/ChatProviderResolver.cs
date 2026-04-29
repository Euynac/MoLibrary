using Monica.AI.Models;

namespace Monica.AI.UI.UIChat.Support;

/// <summary>
/// Helper class for AI Chat page business logic.
/// Extracts reusable logic from page components.
/// </summary>
public static class ChatProviderResolver
{
    /// <summary>
    /// Gets providers that have at least one chat-capable (LLM) model.
    /// </summary>
    public static IReadOnlyList<AIProviderInfo> GetChatProviders(IReadOnlyList<AIProviderInfo> providers)
    {
        return providers
            .Where(provider => provider.IsValid && HasChatModel(provider))
            .ToList();
    }

    /// <summary>
    /// Gets chat-capable models (LLM only) for a provider.
    /// </summary>
    public static IReadOnlyList<AIModelInfo> GetChatModels(AIProviderInfo? provider)
    {
        return provider?.SupportedModels?
            .OfType<LLMModelInfo>()
            .Cast<AIModelInfo>()
            .ToList() ?? [];
    }

    /// <summary>
    /// Resolves the preferred provider for chat usage.
    /// </summary>
    public static AIProviderInfo? GetPreferredChatProvider(
        IReadOnlyList<AIProviderInfo> providers,
        string? preferredProviderId = null)
    {
        var chatProviders = GetChatProviders(providers);
        if (chatProviders.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredProviderId))
        {
            var preferred = chatProviders.FirstOrDefault(
                p => string.Equals(p.ProviderId, preferredProviderId, StringComparison.OrdinalIgnoreCase));
            if (preferred != null)
            {
                return preferred;
            }
        }

        return chatProviders.FirstOrDefault(p => p.IsDefault) ?? chatProviders.First();
    }

    /// <summary>
    /// Resolves the preferred chat model name for a provider.
    /// </summary>
    public static string? GetPreferredChatModel(AIProviderInfo? provider, string? preferredModelName = null)
    {
        var chatModels = provider?.SupportedModels?.OfType<LLMModelInfo>().ToList();
        if (chatModels is not { Count: > 0 })
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredModelName) &&
            chatModels.Any(m => string.Equals(m.ModelName, preferredModelName, StringComparison.OrdinalIgnoreCase)))
        {
            return preferredModelName;
        }

        if (!string.IsNullOrWhiteSpace(provider?.DefaultModel) &&
            chatModels.Any(m => string.Equals(m.ModelName, provider.DefaultModel, StringComparison.OrdinalIgnoreCase)))
        {
            return provider.DefaultModel;
        }

        return chatModels.First().ModelName;
    }

    /// <summary>
    /// Validates whether the provider has at least one chat-capable model.
    /// </summary>
    public static bool HasChatModel(AIProviderInfo? provider)
    {
        return provider?.SupportedModels?.OfType<LLMModelInfo>().Any() ?? false;
    }

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

    /// <summary>
    /// Validates whether the model is a chat-capable (LLM) model on the provider.
    /// </summary>
    public static bool IsChatModelValid(
        AIProviderInfo? provider,
        string? modelName)
    {
        if (provider == null || string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        return provider.SupportedModels?
            .OfType<LLMModelInfo>()
            .Any(m => string.Equals(m.ModelName, modelName, StringComparison.OrdinalIgnoreCase)) ?? false;
    }
}
