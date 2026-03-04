using System;
using System.ClientModel;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Providers;
using Monica.AI.Services;
using Monica.Tool.MoResponse;
using OpenAI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Monica.AI.Providers.OpenAI;

/// <summary>
/// OpenAI Provider 实现
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private const EAIProviderType ProviderKind = EAIProviderType.OpenAI;
    private readonly OpenAIProviderOptions _options;
    private readonly OpenAIClient _client;
    private readonly IReadOnlyList<AIModelInfo> _models;
    private readonly string? _defaultModel;
    private readonly bool _isValid;
    private readonly IReadOnlyList<string> _invalidModels;
    private string? _systemPrompt;
    private readonly ConcurrentDictionary<string, IChatClient> _chatClients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IEmbeddingGenerator<string, Embedding<float>>>
        _embeddingGenerators = new(StringComparer.OrdinalIgnoreCase);
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

        var resolution = AIProviderModelResolver.ResolveModels(modelCatalog, options);
        _models = resolution.Models;
        _defaultModel = resolution.DefaultModel;
        _isValid = resolution.IsValid;
        _invalidModels = resolution.MissingModels;
        _systemPrompt = options.SystemPrompt;
    }

    /// <inheritdoc />
    public string ProviderId => _options.ProviderId ?? ProviderKind.ToString();

    /// <inheritdoc />
    public string ProviderType => ProviderKind.ToString();

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName ?? AIProviderNaming.BuildDisplayName(ProviderType, ProviderId);

    /// <inheritdoc />
    public AIProviderInfo Info => new()
    {
        ProviderId = ProviderId,
        DisplayName = DisplayName,
        Description = $"{ProviderType} GPT models",
        ProviderType = ProviderType,
        DefaultModel = _defaultModel,
        SystemPrompt = _systemPrompt,
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
    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(
        string? modelName = null)
    {
        var resolvedModel = !string.IsNullOrWhiteSpace(modelName)
            ? modelName
            : _models.OfType<EmbeddingModelInfo>().FirstOrDefault()?.ModelName
              ?? throw new NotSupportedException(
                  "No embedding model configured for this OpenAI provider. " +
                  "Add an embedding model to SupportedModels (e.g., 'text-embedding-3-small').");

        return _embeddingGenerators.GetOrAdd(resolvedModel, name =>
            _client.GetEmbeddingClient(name).AsIEmbeddingGenerator());
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

    /// <inheritdoc />
    public async Task<Res<IReadOnlyList<AIRemoteModelInfo>>> FetchRemoteModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var modelClient = _client.GetOpenAIModelClient();
            var result = await modelClient.GetModelsAsync(ct);
            IReadOnlyList<AIRemoteModelInfo> remoteModels = result.Value
                .Select(m => new AIRemoteModelInfo
                {
                    ModelId = m.Id,
                    Metadata = new Dictionary<string, string>
                    {
                        ["OwnedBy"] = m.OwnedBy ?? "",
                        ["CreatedAt"] = m.CreatedAt.ToString("O")
                    }
                })
                .OrderBy(m => m.ModelId)
                .ToList();
            return Res.Ok<IReadOnlyList<AIRemoteModelInfo>>(remoteModels);
        }
        catch (Exception ex)
        {
            return Res.Fail("Failed to fetch OpenAI models: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public void UpdateSystemPrompt(string? systemPrompt)
    {
        _systemPrompt = systemPrompt;
        _options.SystemPrompt = systemPrompt;
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

                foreach (var generator in _embeddingGenerators.Values)
                {
                    (generator as IDisposable)?.Dispose();
                }
                _embeddingGenerators.Clear();
            }
            _disposed = true;
        }
    }
}
