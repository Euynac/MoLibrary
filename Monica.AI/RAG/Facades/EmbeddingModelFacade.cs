using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.KnowledgeBase.Services;
using Monica.AI.Models;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.RAG.Facades;

/// <summary>
/// Host-facing facade for embedding model discovery and knowledge-base binding.
/// </summary>
public class EmbeddingModelFacade(
    IServiceProvider serviceProvider,
    IAIProviderFactory providerFactory,
    KnowledgeBaseService knowledgeBaseService,
    ILogger<EmbeddingModelFacade> logger)
{
    public Task<Res<IReadOnlyList<EmbeddingModelOption>>> GetEmbeddingModelsAsync()
    {
        try
        {
            var options = providerFactory.GetAllProviders()
                .Where(provider => provider.Info.IsValid)
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
                Res.Fail($"Failed to load embedding models: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets diagnostic metadata for every configured embedding model, including invalid providers.
    /// </summary>
    public Task<Res<IReadOnlyList<EmbeddingModelDiagnosticInfo>>> GetEmbeddingModelDiagnosticsAsync()
    {
        try
        {
            var diagnostics = providerFactory.GetAllProviders()
                .SelectMany(provider => provider.Info.SupportedModels?
                    .OfType<EmbeddingModelInfo>()
                    .Select(model => new EmbeddingModelDiagnosticInfo
                    {
                        ProviderId = provider.ProviderId,
                        ProviderDisplayName = provider.DisplayName,
                        ProviderStatus = provider.Info.Status.ToString(),
                        IsProviderValid = provider.Info.IsValid,
                        ModelName = model.ModelName,
                        Dimensions = model.Dimensions,
                        Description = model.Description,
                        ConfigurationErrors = provider.Info.ConfigurationErrors ?? []
                    }) ?? [])
                .GroupBy(
                    option => EmbeddingModelOption.ToModelKey(option.ProviderId, option.ModelName),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(option => option.ProviderDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(option => option.ModelName, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();

            return Task.FromResult(Res.Ok<IReadOnlyList<EmbeddingModelDiagnosticInfo>>(diagnostics));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get embedding model diagnostics.");
            return Task.FromResult<Res<IReadOnlyList<EmbeddingModelDiagnosticInfo>>>(
                Res.Fail($"Failed to load embedding diagnostics: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Tests whether one configured embedding model can generate a vector through its provider.
    /// </summary>
    /// <param name="providerId">Provider identifier that owns the embedding model.</param>
    /// <param name="modelName">Embedding model name to test.</param>
    /// <param name="ct">Cancellation token used for the live embedding request.</param>
    public async Task<Res<EmbeddingModelConnectionTestResult>> TestEmbeddingModelConnectionAsync(
        string providerId,
        string modelName,
        CancellationToken ct = default)
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

            var provider = providerFactory.GetProvider(providerId);
            if (provider is null)
            {
                return Res.Fail($"Embedding provider '{providerId}' was not found.");
            }

            var embeddingModel = provider.Info.SupportedModels?
                .OfType<EmbeddingModelInfo>()
                .FirstOrDefault(model => string.Equals(model.ModelName, modelName, StringComparison.OrdinalIgnoreCase));

            if (embeddingModel is null)
            {
                return Res.Fail($"Embedding model '{modelName}' is not configured on provider '{provider.ProviderId}'.");
            }

            var generator = provider.GetEmbeddingGenerator(embeddingModel.ModelName);
            var generated = await generator.GenerateAsync(["embedding connection test"], cancellationToken: ct);
            var embedding = generated.FirstOrDefault();
            if (embedding is null)
            {
                return Res.Fail("Embedding generator returned no vectors.");
            }

            var dimensions = embedding.Vector.Length;
            embeddingModel.Dimensions = dimensions;

            return Res.Ok(new EmbeddingModelConnectionTestResult
            {
                ProviderId = provider.ProviderId,
                ModelName = embeddingModel.ModelName,
                Succeeded = true,
                Dimensions = dimensions,
                Message = $"Connection succeeded. Dimensions: {dimensions}."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to test embedding model '{ModelName}' on provider '{ProviderId}'.",
                modelName,
                providerId);
            return Res.Ok(new EmbeddingModelConnectionTestResult
            {
                ProviderId = providerId,
                ModelName = modelName,
                Succeeded = false,
                Message = ex.GetMessageRecursively()
            });
        }
    }

    public async Task<Res<EmbeddingModelOption?>> GetKnowledgeBaseEmbeddingModelAsync(string kbId)
    {
        try
        {
            var kb = await knowledgeBaseService.GetByIdAsync(kbId);
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

            var provider = providerFactory.GetProvider(kb.EmbeddingProviderId);
            var fallbackProviderId = kb.EmbeddingProviderId.Trim();

            return Res.Ok<EmbeddingModelOption?>(new EmbeddingModelOption
            {
                ProviderId = kb.EmbeddingProviderId,
                ProviderDisplayName = provider?.DisplayName ?? fallbackProviderId,
                ModelName = kb.EmbeddingModelName,
                Description = "Embedding model is persisted in KB metadata but unavailable in current providers."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get embedding model for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to get embedding model: {ex.GetMessageRecursively()}");
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

            await GetRagService().SetKnowledgeBaseEmbeddingModelAsync(
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
            return ex is InvalidOperationException or KeyNotFoundException
                ? Res.Fail(ex.GetMessageRecursively())
                : Res.Fail($"Failed to set embedding model: {ex.GetMessageRecursively()}");
        }
    }

    // Delay RAG service resolution so the page can load provider metadata
    // without constructing the vector-store pipeline on entry.
    private RAGService GetRagService()
        => serviceProvider.GetRequiredService<RAGService>();
}
