using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.RAG.Services;
using Monica.AI.UI.Models;
using Monica.Tool.MoResponse;

namespace Monica.AI.UI.Services;

/// <summary>
/// UI service for embedding model management and KB-level embedding binding.
/// </summary>
public class EmbeddingModelManagementUIService(
    RAGService ragService,
    IAIProviderFactory providerFactory,
    ILogger<EmbeddingModelManagementUIService> logger) : IEmbeddingModelManagementUIService
{
    public Task<Res<IReadOnlyList<EmbeddingModelOption>>> GetEmbeddingModelsAsync()
    {
        try
        {
            var options = providerFactory.GetAllProviders()
                .SelectMany(provider => provider.Info.SupportedModels?
                    .OfType<EmbeddingModelInfo>()
                    .Select(model => new EmbeddingModelOption
                    {
                        ProviderId = provider.ProviderId,
                        ProviderDisplayName = provider.DisplayName,
                        ModelName = model.ModelName,
                        Dimensions = model.Dimensions,
                        Description = model.Description
                    }) ?? [])
                .GroupBy(
                    option => EmbeddingModelOption.ToModelKey(option.ProviderId, option.ModelName),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(option => option.ProviderDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(option => option.ModelName, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();

            return Task.FromResult(Res.Ok<IReadOnlyList<EmbeddingModelOption>>(options));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get embedding model options.");
            return Task.FromResult<Res<IReadOnlyList<EmbeddingModelOption>>>(
                Res.Fail($"Failed to load embedding models: {ex.Message}"));
        }
    }

    public async Task<Res<EmbeddingModelOption?>> GetKnowledgeBaseEmbeddingModelAsync(string kbId)
    {
        try
        {
            var kb = await ragService.GetKnowledgeBaseByIdAsync(kbId);
            if (kb is null)
            {
                return Res.Fail("Knowledge base not found.");
            }

            if (string.IsNullOrWhiteSpace(kb.EmbeddingProviderId)
                || string.IsNullOrWhiteSpace(kb.EmbeddingModelName))
            {
                return Res.Ok<EmbeddingModelOption?>(null);
            }

            var modelsResult = await GetEmbeddingModelsAsync();
            if (modelsResult.IsFailed(out var error, out var models))
            {
                return Res.Fail(error.Message!);
            }

            var model = models.FirstOrDefault(m =>
                string.Equals(m.ProviderId, kb.EmbeddingProviderId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.ModelName, kb.EmbeddingModelName, StringComparison.OrdinalIgnoreCase));

            if (model is not null)
            {
                return Res.Ok<EmbeddingModelOption?>(model);
            }

            return Res.Ok<EmbeddingModelOption?>(new EmbeddingModelOption
            {
                ProviderId = kb.EmbeddingProviderId,
                ProviderDisplayName = kb.EmbeddingProviderId,
                ModelName = kb.EmbeddingModelName,
                Description = "Embedding model is persisted in KB metadata but unavailable in current providers."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get embedding model for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to get embedding model: {ex.Message}");
        }
    }

    public async Task<Res> SetKnowledgeBaseEmbeddingModelAsync(
        string kbId,
        string providerId,
        string modelName,
        bool clearIndex = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return Res.Fail("Embedding provider ID cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(modelName))
            {
                return Res.Fail("Embedding model name cannot be empty.");
            }

            var modelsResult = await GetEmbeddingModelsAsync();
            if (modelsResult.IsFailed(out var error, out var models))
            {
                return Res.Fail(error.Message!);
            }

            var exists = models.Any(option =>
                string.Equals(option.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(option.ModelName, modelName, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                return Res.Fail(
                    $"Embedding model '{modelName}' is not available on provider '{providerId}'.");
            }

            await ragService.SetKnowledgeBaseEmbeddingModelAsync(
                kbId,
                providerId,
                modelName,
                clearIndex);

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to set embedding binding for KB '{KbId}' to provider '{ProviderId}' model '{ModelName}'",
                kbId, providerId, modelName);
            return Res.Fail($"Failed to set embedding model: {ex.Message}");
        }
    }
}
