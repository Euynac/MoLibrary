namespace Monica.AI.Providers;

/// <summary>
/// AI Provider configuration base class
/// </summary>
public abstract class AIProviderOptions
{
    /// <summary>
    /// Provider unique identifier, or Provider name if not set
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Provider display name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// API key
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Provider default system prompt word
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// List of supported models (if empty, the Provider is invalid)
    /// </summary>
    public IList<string>? SupportedModels { get; set; }

    /// <summary>
    /// API base URL (optional, for custom endpoints)
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Whether to set it as the default Provider
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Request timeout (seconds)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// OpenAI Provider configuration
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
/// Anthropic Provider Configuration
/// </summary>
public class AnthropicProviderOptions : AIProviderOptions
{
    /// <summary>
    /// Default maximum number of Tokens
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
}
