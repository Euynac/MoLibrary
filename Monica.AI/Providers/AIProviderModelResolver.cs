using Monica.AI.Models;
using Monica.AI.Services;

namespace Monica.AI.Providers;

internal static class AIProviderModelResolver
{
    public static ProviderModelResolution ResolveModels(
        EAIProviderType providerType,
        AIModelCatalog catalog,
        AIProviderOptions options)
    {
        var supportedModels = options.SupportedModels
            ?.Where(modelName => !string.IsNullOrWhiteSpace(modelName))
            .Select(modelName => modelName!.Trim())
            .ToList()
            ?? [];

        if (supportedModels.Count == 0)
        {
            return new ProviderModelResolution(
                [],
                [],
                null,
                false);
        }

        var result = new List<AIModelInfo>();
        var missingModels = new List<string>();

        foreach (var modelName in supportedModels)
        {
            var model = catalog.GetModel(providerType, modelName);
            if (model == null)
            {
                missingModels.Add(modelName);
                continue;
            }

            result.Add(model);
        }

        var defaultModel = supportedModels.FirstOrDefault();
        var isValid = missingModels.Count == 0 && result.Count > 0;

        return new ProviderModelResolution(
            result,
            missingModels,
            defaultModel,
            isValid);
    }
}

internal sealed record ProviderModelResolution(
    IReadOnlyList<AIModelInfo> Models,
    IReadOnlyList<string> MissingModels,
    string? DefaultModel,
    bool IsValid);
