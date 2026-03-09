using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.RAG.Services;
using Monica.AI.Services;

namespace Monica.AI.Providers.Fake;

/// <summary>
/// Fake AI provider used for embedding-only development and testing.
/// </summary>
public class FakeProvider : IAIProvider
{
    private const EAIProviderType ProviderKind = EAIProviderType.Fake;
    private readonly FakeProviderOptions _options;
    private readonly IReadOnlyList<AIModelInfo> _models;
    private readonly string? _defaultModel;
    private readonly bool _isValid;
    private readonly IReadOnlyList<string> _invalidModels;
    private readonly ConcurrentDictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _generators = new(StringComparer.OrdinalIgnoreCase);
    private string? _systemPrompt;
    private bool _disposed;

    public FakeProvider(FakeProviderOptions options, AIModelCatalog modelCatalog)
    {
        _options = options;

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
        Description = "Fake embedding provider for development and testing.",
        ProviderType = ProviderType,
        DefaultModel = _defaultModel,
        SystemPrompt = _systemPrompt,
        SupportedModels = _models,
        IsValid = _isValid,
        InvalidModels = _invalidModels,
        IsDefault = _options.IsDefault,
        Icon = "science",
        SupportsRemoteModelListing = false
    };

    /// <inheritdoc />
    public bool SupportsRemoteModelListing => false;

    /// <inheritdoc />
    public IChatClient GetChatClient(string? modelName = null)
    {
        throw new NotSupportedException(
            "FakeProvider does not support chat. It is embedding-only.");
    }

    /// <inheritdoc />
    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(string? modelName = null)
    {
        var resolvedModel = !string.IsNullOrWhiteSpace(modelName)
            ? modelName
            : _models.OfType<EmbeddingModelInfo>().FirstOrDefault()?.ModelName
              ?? throw new NotSupportedException(
                  "No embedding model configured for FakeProvider. Add one to SupportedModels.");

        return _generators.GetOrAdd(resolvedModel, name =>
        {
            var dimensions = _models
                .OfType<EmbeddingModelInfo>()
                .FirstOrDefault(m => string.Equals(m.ModelName, name, StringComparison.OrdinalIgnoreCase))
                ?.Dimensions ?? _options.DefaultDimensions;

            return new FakeEmbeddingGenerator(dimensions);
        });
    }

    /// <inheritdoc />
    public Task TestConnectionAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> models = _models.Select(m => m.ModelName).ToList();
        return Task.FromResult(models);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AIRemoteModelInfo>> FetchRemoteModelsAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("Fake provider does not support remote model listing.");
    }

    /// <inheritdoc />
    public void UpdateSystemPrompt(string? systemPrompt)
    {
        _systemPrompt = systemPrompt;
        _options.SystemPrompt = systemPrompt;
    }

    /// <inheritdoc />
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
                foreach (var generator in _generators.Values)
                {
                    (generator as IDisposable)?.Dispose();
                }

                _generators.Clear();
            }

            _disposed = true;
        }
    }
}
