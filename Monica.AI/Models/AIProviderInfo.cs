namespace Monica.AI.Models;

/// <summary>
/// AI Provider metadata information
/// </summary>
public class AIProviderInfo
{
    /// <summary>
    /// Provider unique identifier
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Provider display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Provider Description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Provider type (such as OpenAI, Anthropic, etc.)
    /// </summary>
    public required string ProviderType { get; init; }

    /// <summary>
    /// The model used by default
    /// </summary>
    public string? DefaultModel { get; init; }

    /// <summary>
    /// Default system prompt word
    /// </summary>
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Model metadata information
    /// </summary>
    public IReadOnlyList<AIModelInfo>? SupportedModels { get; init; }

    /// <summary>
    /// Is the Provider valid (the model configuration is complete)
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    /// Missing model name
    /// </summary>
    public IReadOnlyList<string>? InvalidModels { get; init; }

    /// <summary>
    /// Whether it is the default Provider
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Icon (for UI display)
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Provider status
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
/// Provider status enum
/// </summary>
public enum AIProviderStatus
{
    /// <summary>
    /// unknown status
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
