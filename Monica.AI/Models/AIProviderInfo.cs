namespace Monica.AI.Models;

/// <summary>
/// Metadata for an AI provider.
/// </summary>
public class AIProviderInfo
{
    /// <summary>
    /// Unique provider identifier.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Provider display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Provider description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Provider type, such as OpenAI or Anthropic.
    /// </summary>
    public required string ProviderType { get; init; }

    /// <summary>
    /// Default model used by the provider.
    /// </summary>
    public string? DefaultModel { get; init; }

    /// <summary>
    /// Default system prompt.
    /// </summary>
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Model metadata.
    /// </summary>
    public IReadOnlyList<AIModelInfo>? SupportedModels { get; init; }

    /// <summary>
    /// Indicates whether the provider is valid, meaning its model configuration is complete.
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    /// Names of missing models.
    /// </summary>
    public IReadOnlyList<string>? InvalidModels { get; init; }

    /// <summary>
    /// Indicates whether this is the default provider.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Icon used for UI display.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Provider status.
    /// </summary>
    public AIProviderStatus Status { get; set; } = AIProviderStatus.Unknown;

    /// <summary>
    /// Whether this provider supports fetching remote model lists.
    /// </summary>
    public bool SupportsRemoteModelListing { get; init; } = true;

    /// <summary>
    /// Model information fetched from the remote provider API.
    /// Null if not yet fetched or provider doesn't support it.
    /// </summary>
    public IReadOnlyList<AIRemoteModelInfo>? RemoteModels { get; set; }
}

/// <summary>
/// Provider status. enum
/// </summary>
public enum AIProviderStatus
{
    /// <summary>
    /// Unknown status.
    /// </summary>
    Unknown,

    /// <summary>
    /// Available
    /// </summary>
    Available,

    /// <summary>
    /// Not available
    /// </summary>
    Unavailable,

    /// <summary>
    /// Configuration error
    /// </summary>
    ConfigurationError
}
