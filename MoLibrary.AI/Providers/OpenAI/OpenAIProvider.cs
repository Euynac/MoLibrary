using System;
using System.ClientModel;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using MoLibrary.AI.Providers;
using MoLibrary.AI.Services;
using MoLibrary.Tool.MoResponse;
using OpenAI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace MoLibrary.AI.Providers.OpenAI;

/// <summary>
/// OpenAI Provider 实现
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly OpenAIProviderOptions _options;
    private readonly OpenAIClient _client;
    private readonly IReadOnlyList<AIModelInfo> _models;
    private readonly string? _defaultModel;
    private readonly bool _isValid;
    private readonly IReadOnlyList<string> _invalidModels;
    private readonly ConcurrentDictionary<string, IChatClient> _chatClients = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public OpenAIProvider(OpenAIProviderOptions options, AIModelCatalog modelCatalog)
    {
        _options = options;

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(options.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(options.BaseUrl);
        }

        _client = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);

        var resolution = AIProviderModelResolver.ResolveModels(EAIProviderType.OpenAI, modelCatalog, options);
        _models = resolution.Models;
        _defaultModel = resolution.DefaultModel;
        _isValid = resolution.IsValid;
        _invalidModels = resolution.MissingModels;
    }

    /// <inheritdoc />
    public string ProviderId => _options.ProviderId ?? (_defaultModel == null ? "openai" : $"openai-{_defaultModel}");

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName ?? (_defaultModel == null ? "OpenAI" : $"OpenAI ({_defaultModel})");

    /// <inheritdoc />
    public AIProviderInfo Info => new()
    {
        ProviderId = ProviderId,
        DisplayName = DisplayName,
        Description = "OpenAI GPT models",
        ProviderType = "OpenAI",
        DefaultModel = _defaultModel,
        SupportedModels = _models,
        IsValid = _isValid,
        InvalidModels = _invalidModels,
        IsDefault = _options.IsDefault,
        Icon = "openai"
    };

    /// <inheritdoc />
    public IChatClient GetChatClient(string? modelName = null)
    {
        var resolvedModel = !string.IsNullOrWhiteSpace(modelName) ? modelName : _defaultModel;
        if (string.IsNullOrWhiteSpace(resolvedModel))
        {
            throw new InvalidOperationException("OpenAI model is not configured.");
        }

        return _chatClients.GetOrAdd(resolvedModel, name => _client.GetChatClient(name).AsIChatClient());
    }

    /// <inheritdoc />
    public async Task<Res> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var chatClient = GetChatClient();
            var response = await chatClient.GetResponseAsync(
                [new AIChatMessage(ChatRole.User, "Hello")],
                new ChatOptions { MaxOutputTokens = 10 },
                ct);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail($"OpenAI connection test failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Res<IReadOnlyList<string>>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var models = _models.Select(m => m.ModelName).ToList();
        return Task.FromResult(Res.Ok<IReadOnlyList<string>>(models));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var chatClient in _chatClients.Values)
                {
                    chatClient.Dispose();
                }
                _chatClients.Clear();
            }
            _disposed = true;
        }
    }
}
