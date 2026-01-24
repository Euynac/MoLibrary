using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using System;
using Microsoft.Extensions.AI;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AI.UI.Services;

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

        return provider.TestConnectionAsync(ct);
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
}
