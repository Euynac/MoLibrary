using Monica.AI.Abstractions;
using Monica.AI.Models;
using Microsoft.Extensions.AI;
using Monica.Tool.MoResponse;

namespace Monica.AI.UI.Services;

/// <summary>
/// AI Provider UI 服务
/// </summary>
public class AIProviderUIService(IAIProviderFactory providerFactory)
{
    public IReadOnlyList<AIProviderInfo> GetProviders()
    {
        return providerFactory.GetAllProviderInfos();
    }

    public Task<Res> TestProviderAsync(string providerId, CancellationToken ct = default)
    {
        var provider = providerFactory.GetProvider(providerId);
        if (provider == null)
        {
            return Task.FromResult(Res.Fail($"Provider '{providerId}' not found"));
        }

        return TestProviderCoreAsync(provider, ct);
    }

    private static async Task<Res> TestProviderCoreAsync(IAIProvider provider, CancellationToken ct)
    {
        try
        {
            await provider.TestConnectionAsync(ct);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.Message);
        }
    }

    public async Task<Res> TestModelAsync(string providerId, string modelName, CancellationToken ct = default)
    {
        var provider = providerFactory.GetProvider(providerId);
        if (provider == null)
        {
            return Res.Fail($"Provider '{providerId}' not found");
        }

        var model = provider.Info.SupportedModels?.FirstOrDefault(m =>
            string.Equals(m.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        if (model is not LLMModelInfo)
        {
            return Res.Fail("Only LLM models are supported for testing");
        }

        try
        {
            var chatClient = provider.GetChatClient(modelName);
            await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Hello")],
                new ChatOptions { MaxOutputTokens = 8 },
                ct);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail($"Model test failed: {ex.Message}");
        }
    }

    public Res UpdateProviderSystemPrompt(string providerId, string? systemPrompt)
    {
        var provider = providerFactory.GetProvider(providerId);
        if (provider == null)
        {
            return Res.Fail($"Provider '{providerId}' not found");
        }

        provider.UpdateSystemPrompt(systemPrompt);
        return Res.Ok();
    }

    public async Task<Res<IReadOnlyList<AIRemoteModelInfo>>> FetchRemoteModelsAsync(
        string providerId, CancellationToken ct = default)
    {
        var provider = providerFactory.GetProvider(providerId);
        if (provider == null)
        {
            return Res.Fail($"Provider '{providerId}' not found");
        }

        if (!provider.SupportsRemoteModelListing)
        {
            return Res.Fail("Provider does not support remote model listing");
        }

        try
        {
            var models = await provider.FetchRemoteModelsAsync(ct);
            return Res.Ok(models);
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.Message);
        }
    }

    public bool SupportsRemoteModelListing(string providerId)
    {
        var provider = providerFactory.GetProvider(providerId);
        return provider?.SupportsRemoteModelListing ?? false;
    }

    /// <summary>
    /// Probes the actual vector dimensions of an embedding model by sending a short text
    /// and measuring the returned embedding length. Updates the model's Dimensions if successful.
    /// </summary>
    public async Task<Res<int>> ProbeEmbeddingDimensionsAsync(
        string providerId, string modelName, CancellationToken ct = default)
    {
        var provider = providerFactory.GetProvider(providerId);
        if (provider == null)
            return Res.Fail($"Provider '{providerId}' not found");

        var model = provider.Info.SupportedModels?.FirstOrDefault(m =>
            string.Equals(m.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        if (model is not EmbeddingModelInfo embeddingModel)
            return Res.Fail("Only embedding models can be probed for dimensions");

        try
        {
            var generator = provider.GetEmbeddingGenerator(modelName);
            var result = await generator.GenerateAsync(["dimension probe"], cancellationToken: ct);
            var dimensions = result[0].Vector.Length;

            // Update the model's dimensions in place
            embeddingModel.Dimensions = dimensions;

            return Res.Ok(dimensions);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Embedding probe failed: {ex.Message}");
        }
    }
}
