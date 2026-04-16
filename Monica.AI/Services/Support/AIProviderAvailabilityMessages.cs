using Monica.AI.Models;

namespace Monica.AI.Services.Support;

internal static class AIProviderAvailabilityMessages
{
    public static string BuildProviderUnavailableMessage(AIProviderInfo info)
    {
        var providerName = string.IsNullOrWhiteSpace(info.DisplayName) ? info.ProviderId : info.DisplayName;
        var reasons = new List<string>();

        if (info.ConfigurationErrors is { Count: > 0 })
        {
            reasons.AddRange(info.ConfigurationErrors.Where(error => !string.IsNullOrWhiteSpace(error)));
        }

        if (info.InvalidModels is { Count: > 0 })
        {
            reasons.Add($"Missing configured models: {string.Join(", ", info.InvalidModels)}.");
        }
        else if (!info.IsValid
                 && info.ConfigurationErrors is not { Count: > 0 }
                 && info.SupportedModels is not { Count: > 0 })
        {
            reasons.Add("No supported models are configured.");
        }

        var detail = reasons.Count > 0
            ? string.Join(" ", reasons)
            : "Check the provider configuration.";

        return $"Provider '{providerName}' is disabled. {detail}";
    }

    public static string BuildNoEnabledProviderMessage(IReadOnlyList<AIProviderInfo> providers)
    {
        if (providers.Count == 0)
        {
            return "No AI provider is registered.";
        }

        var disabledProviders = providers
            .Where(provider => !provider.IsValid)
            .Select(provider => string.IsNullOrWhiteSpace(provider.DisplayName) ? provider.ProviderId : provider.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (disabledProviders.Count == 0)
        {
            return "No enabled AI provider is available.";
        }

        return $"No enabled AI provider is available. Disabled providers: {string.Join(", ", disabledProviders)}.";
    }
}
