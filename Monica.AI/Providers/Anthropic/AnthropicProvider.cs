using Anthropic;
using Anthropic.Models.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Services;

namespace Monica.AI.Providers.Anthropic;

/// <summary>
/// Anthropic Provider implementation.
/// </summary>
internal sealed class AnthropicProvider : IAIProvider
{
    private const EAIProviderType ProviderKind = EAIProviderType.Anthropic;
    private readonly AnthropicProviderOptions _options;
    private readonly AnthropicClient _client;
    private readonly IReadOnlyList<AIModelInfo> _models;
    private readonly string? _defaultModel;
    private readonly bool _isValid;
    private readonly IReadOnlyList<string> _invalidModels;
    private string? _systemPrompt;
    private readonly ConcurrentDictionary<string, IChatClient> _chatClients = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public AnthropicProvider(AnthropicProviderOptions options, AIModelCatalog modelCatalog)
    {
        _options = options;

        // Anthropic SDK v12 configures the client using an object initializer.
        _client = new AnthropicClient
        {
            ApiKey = options.ApiKey,
            BaseUrl = options.BaseUrl ?? ""
        };

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
        Description = $"{ProviderType} Claude models",
        ProviderType = ProviderType,
        DefaultModel = _defaultModel,
        SystemPrompt = _systemPrompt,
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
    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(
        string? modelName = null)
    {
        throw new NotSupportedException(
            "Anthropic does not provide embedding models. " +
            "Use an OpenAI provider for embedding generation.");
    }

    /// <inheritdoc />
    public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var chatClient = GetChatClient();
            _ = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Hello")],
                new ChatOptions {MaxOutputTokens = 10},
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Anthropic connection test failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> models = _models.Select(m => m.ModelName).ToList();
        return Task.FromResult(models);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AIRemoteModelInfo>> FetchRemoteModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var remoteModels = new List<AIRemoteModelInfo>();
            var page = await _client.Models.List(new ModelListParams { Limit = 1000 }, ct);

            void CollectModels(ModelListPage p)
            {
                foreach (var model in p.Items)
                {
                    remoteModels.Add(new AIRemoteModelInfo
                    {
                        ModelId = model.ID,
                        Metadata = new Dictionary<string, string>
                        {
                            ["DisplayName"] = model.DisplayName ?? "",
                            ["CreatedAt"] = model.CreatedAt.ToString("O")
                        }
                    });
                }
            }

            CollectModels(page);

            while (page.HasNext())
            {
                page = await page.Next(ct);
                CollectModels(page);
            }

            remoteModels.Sort((a, b) => string.Compare(a.ModelId, b.ModelId, StringComparison.Ordinal));
            return remoteModels;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to fetch Anthropic models: " + ex.Message, ex);
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
        if (_disposed)
        {
            return;
        }

        foreach (var chatClient in _chatClients.Values)
        {
            chatClient.Dispose();
        }

        _chatClients.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
