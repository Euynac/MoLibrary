using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Resolves and validates embedding binding for knowledge bases.
/// </summary>
public sealed class RAGEmbeddingBindingResolver(
    IAIProviderFactory providerFactory,
    ILogger<RAGEmbeddingBindingResolver> logger)
{
    private readonly ConcurrentDictionary<string, int> _resolvedEmbeddingDimensions =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<RAGEmbeddingBinding> ResolveAsync(KnowledgeBaseModel kb, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kb.EmbeddingProviderId) || string.IsNullOrWhiteSpace(kb.EmbeddingModelName))
        {
            throw new InvalidOperationException(
                $"Knowledge base '{kb.Name}' has no embedding model configured. Configure provider and model before indexing or searching.");
        }

        return await ResolveAsync(kb.EmbeddingProviderId, kb.EmbeddingModelName, ct);
    }

    public async Task<RAGEmbeddingBinding> ResolveAsync(
        string providerId,
        string modelName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new InvalidOperationException("Embedding provider ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new InvalidOperationException("Embedding model name cannot be empty.");
        }

        var provider = providerFactory.GetProvider(providerId)
                       ?? throw new InvalidOperationException(
                           $"Embedding provider '{providerId}' was not found.");

        var embeddingModel = FindEmbeddingModel(provider, modelName)
                             ?? throw new InvalidOperationException(
                                 $"Embedding model '{modelName}' is not configured on provider '{provider.ProviderId}'.");

        var dimensions = await ResolveEmbeddingDimensionsAsync(provider, embeddingModel, ct);
        return new RAGEmbeddingBinding(provider.ProviderId, embeddingModel.ModelName, dimensions);
    }

    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(RAGEmbeddingBinding binding)
    {
        var provider = providerFactory.GetProvider(binding.ProviderId)
                       ?? throw new InvalidOperationException(
                           $"Embedding provider '{binding.ProviderId}' not found.");

        return provider.GetEmbeddingGenerator(binding.ModelName);
    }

    private async Task<int> ResolveEmbeddingDimensionsAsync(
        IAIProvider provider,
        EmbeddingModelInfo embeddingModel,
        CancellationToken ct)
    {
        if (embeddingModel.Dimensions is > 0)
        {
            return embeddingModel.Dimensions.Value;
        }

        var dimensionCacheKey = $"{provider.ProviderId}::{embeddingModel.ModelName}";
        if (_resolvedEmbeddingDimensions.TryGetValue(dimensionCacheKey, out var cachedDimensions) && cachedDimensions > 0)
        {
            embeddingModel.Dimensions = cachedDimensions;
            return cachedDimensions;
        }

        logger.LogInformation(
            "Embedding model '{ModelName}' on provider '{ProviderId}' has no configured dimensions. Probing dimensions from runtime generator.",
            embeddingModel.ModelName,
            provider.ProviderId);

        var generator = provider.GetEmbeddingGenerator(embeddingModel.ModelName);
        var generated = await generator.GenerateAsync(["dimension-probe"], cancellationToken: ct);
        var firstEmbedding = generated.FirstOrDefault();
        if (firstEmbedding is null)
        {
            throw new InvalidOperationException(
                $"Embedding generator returned no vectors for model '{embeddingModel.ModelName}' on provider '{provider.ProviderId}'.");
        }

        var dimensions = firstEmbedding.Vector.Length;
        if (dimensions <= 0)
        {
            throw new InvalidOperationException(
                $"Failed to resolve dimensions for embedding model '{embeddingModel.ModelName}' on provider '{provider.ProviderId}'.");
        }

        embeddingModel.Dimensions = dimensions;
        _resolvedEmbeddingDimensions[dimensionCacheKey] = dimensions;

        logger.LogInformation(
            "Resolved embedding dimensions for provider '{ProviderId}', model '{ModelName}': {Dimensions}",
            provider.ProviderId,
            embeddingModel.ModelName,
            dimensions);

        return dimensions;
    }

    private static EmbeddingModelInfo? FindEmbeddingModel(IAIProvider provider, string modelName)
    {
        return provider.Info.SupportedModels?
            .OfType<EmbeddingModelInfo>()
            .FirstOrDefault(model =>
                string.Equals(model.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
    }
}
