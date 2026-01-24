using System;
using Anthropic;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using MoLibrary.AI.Providers;
using MoLibrary.AI.Services;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AI.Providers.Anthropic;

/// <summary>
/// Anthropic Provider 实现
/// </summary>
public class AnthropicProvider : IAIProvider
{
    private readonly AnthropicProviderOptions _options;
    private readonly AnthropicClient _client;
    private readonly IReadOnlyList<AIModelInfo> _models;
    private readonly string? _defaultModel;
    private readonly bool _isValid;
    private readonly IReadOnlyList<string> _invalidModels;
    private readonly ConcurrentDictionary<string, IChatClient> _chatClients = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public AnthropicProvider(AnthropicProviderOptions options, AIModelCatalog modelCatalog)
    {
        _options = options;

        // Anthropic SDK v12 使用对象初始化器配置客户端
        _client = new AnthropicClient
        {
            ApiKey = options.ApiKey,
            BaseUrl = options.BaseUrl ?? ""
        };

        var resolution = AIProviderModelResolver.ResolveModels(EAIProviderType.Anthropic, modelCatalog, options);
        _models = resolution.Models;
        _defaultModel = resolution.DefaultModel;
        _isValid = resolution.IsValid;
        _invalidModels = resolution.MissingModels;
    }

    /// <inheritdoc />
    public string ProviderId => _options.ProviderId ?? (_defaultModel == null ? "anthropic" : $"anthropic-{_defaultModel}");

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName ?? (_defaultModel == null ? "Anthropic" : $"Anthropic ({_defaultModel})");

    /// <inheritdoc />
    public AIProviderInfo Info => new()
    {
        ProviderId = ProviderId,
        DisplayName = DisplayName,
        Description = "Anthropic Claude models",
        ProviderType = "Anthropic",
        DefaultModel = _defaultModel,
        SupportedModels = _models,
        IsValid = _isValid,
        InvalidModels = _invalidModels,
        IsDefault = _options.IsDefault,
        Icon = "anthropic"
    };

    /// <inheritdoc />
    public IChatClient GetChatClient(string? modelName = null)
    {
        var resolvedModel = !string.IsNullOrWhiteSpace(modelName) ? modelName : _defaultModel;
        if (string.IsNullOrWhiteSpace(resolvedModel))
        {
            throw new InvalidOperationException("Anthropic model is not configured.");
        }

        return _chatClients.GetOrAdd(resolvedModel, name => _client.AsIChatClient(name));
    }

    /// <inheritdoc />
    public async Task<Res> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var chatClient = GetChatClient();
            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Hello")],
                new ChatOptions {MaxOutputTokens = 10},
                ct);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail($"Anthropic connection test failed: {ex.Message}");
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
