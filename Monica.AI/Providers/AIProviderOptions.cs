namespace Monica.AI.Providers;

/// <summary>
/// Base class for AI provider configuration.
/// </summary>
public abstract class AIProviderOptions
{
    /// <summary>
    /// Unique provider identifier., or Provider name if not set
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Provider display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// API key
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Default system prompt for the provider.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// List of supported models. If empty, the provider is considered invalid.
    /// </summary>
    public IList<string>? SupportedModels { get; set; }

    /// <summary>
    /// Base API URL for custom endpoints, if applicable.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Indicates whether this provider should be set as the default.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// OpenAI provider configuration.
/// </summary>
public class OpenAIProviderOptions : AIProviderOptions
{
    /// <summary>
    /// Organization ID (optional)
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Project ID (optional)
    /// </summary>
    public string? Project { get; set; }
}

/// <summary>
/// Anthropic provider configuration.
/// </summary>
public class AnthropicProviderOptions : AIProviderOptions
{
    /// <summary>
    /// Default maximum number of tokens.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
}
