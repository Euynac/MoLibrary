using Microsoft.Extensions.AI;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Services;
using Monica.AI.Services.Support;

namespace Monica.AI.Providers;

/// <summary>
/// Disabled provider placeholder used when provider registration fails because of configuration errors.
/// </summary>
internal sealed class DisabledAIProvider : IAIProvider
{
    private readonly AIProviderOptions _options;
    private readonly string _providerType;
    private readonly string _description;
    private readonly string _icon;
    private readonly IReadOnlyList<AIModelInfo> _models;
    private readonly IReadOnlyList<string> _invalidModels;
    private readonly IReadOnlyList<string> _configurationErrors;
    private readonly string? _defaultModel;
    private readonly bool _supportsRemoteModelListing;
    private string? _systemPrompt;

    private DisabledAIProvider(
        AIProviderOptions options,
        string providerType,
        string description,
        string icon,
        ProviderModelResolution resolution,
        IReadOnlyList<string> configurationErrors,
        bool supportsRemoteModelListing)
    {
        _options = options;
        _providerType = providerType;
        _description = description;
        _icon = icon;
        _models = resolution.Models;
        _invalidModels = resolution.MissingModels;
        _defaultModel = resolution.DefaultModel;
        _configurationErrors = configurationErrors;
        _supportsRemoteModelListing = supportsRemoteModelListing;
        _systemPrompt = options.SystemPrompt;
    }

    public static DisabledAIProvider FromOptions(
        AIProviderOptions options,
        AIModelCatalog modelCatalog,
        string providerType,
        string description,
        string icon,
        IReadOnlyList<string> configurationErrors,
        bool supportsRemoteModelListing = true)
    {
        var resolution = AIProviderModelResolver.ResolveModels(modelCatalog, options);
        return new DisabledAIProvider(
            options,
            providerType,
            description,
            icon,
            resolution,
            configurationErrors,
            supportsRemoteModelListing);
    }

    /// <inheritdoc />
    public string ProviderId => _options.ProviderId ?? _providerType;

    /// <inheritdoc />
    public string ProviderType => _providerType;

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName ?? AIProviderNaming.BuildDisplayName(ProviderType, ProviderId);

    /// <inheritdoc />
    public AIProviderInfo Info => new()
    {
        ProviderId = ProviderId,
        DisplayName = DisplayName,
        Description = _description,
        ProviderType = ProviderType,
        DefaultModel = _defaultModel,
        SystemPrompt = _systemPrompt,
        SupportedModels = _models,
        IsValid = false,
        InvalidModels = _invalidModels,
        ConfigurationErrors = _configurationErrors,
        IsDefault = _options.IsDefault,
        Icon = _icon,
        Status = AIProviderStatus.ConfigurationError,
        SupportsRemoteModelListing = _supportsRemoteModelListing
    };

    /// <inheritdoc />
    public bool SupportsRemoteModelListing => _supportsRemoteModelListing;

    /// <inheritdoc />
    public IChatClient GetChatClient(string? modelName = null)
    {
        throw CreateDisabledException();
    }

    /// <inheritdoc />
    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(string? modelName = null)
    {
        throw CreateDisabledException();
    }

    /// <inheritdoc />
    public Task TestConnectionAsync(CancellationToken ct = default)
    {
        throw CreateDisabledException();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> models = _models.Select(model => model.ModelName).ToList();
        return Task.FromResult(models);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AIRemoteModelInfo>> FetchRemoteModelsAsync(CancellationToken ct = default)
    {
        throw CreateDisabledException();
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
    }

    private InvalidOperationException CreateDisabledException()
    {
        return new InvalidOperationException(AIProviderAvailabilityMessages.BuildProviderUnavailableMessage(Info));
    }
}
