using MoLibrary.AI.Models;
using MoLibrary.AI.Services;

namespace MoLibrary.AI.Providers;

internal static class AIProviderModelResolver
{
    public static IReadOnlyList<AIModelInfo> ResolveModels(
        EAIProviderType providerType,
        AIModelCatalog catalog,
        AIProviderOptions options)
    {
        if (options.SupportedModels is { Count: > 0 })
        {
            var result = new List<AIModelInfo>();
            foreach (var modelName in options.SupportedModels)
            {
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    continue;
                }

                var model = catalog.GetModel(providerType, modelName) ?? new LLMModelInfo
                {
                    ModelName = modelName
                };
                result.Add(model);
            }

            return result;
        }

        return catalog.GetModels(providerType);
    }

    public static string? ResolveDefaultModel(AIProviderOptions options, IReadOnlyList<AIModelInfo> models)
    {
        if (!string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            return options.DefaultModel;
        }

        return models.FirstOrDefault()?.ModelName;
    }
}
