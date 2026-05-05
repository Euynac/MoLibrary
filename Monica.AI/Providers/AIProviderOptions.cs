namespace Monica.AI.Providers;

/// <summary>
/// OpenAI API surface used by the provider for chat requests.
/// </summary>
public enum OpenAIProviderApiMode
{
    /// <summary>
    /// Use the OpenAI Responses API through the Microsoft.Extensions.AI adapter.
    /// </summary>
    Responses,

    /// <summary>
    /// Use the OpenAI Chat Completions API through the Microsoft.Extensions.AI adapter.
    /// </summary>
    Chat
}

/// <summary>
/// Base class for AI provider configuration.
/// </summary>
public abstract class AIProviderOptions
{
    /// <summary>
    /// Unique provider identifier, or provider name if not set.
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
    /// API surface used for chat requests. Defaults to <see cref="OpenAIProviderApiMode.Responses"/>.
    /// The Responses API is preferred because OpenAI prompt caching is automatic for eligible long
    /// prompts and Responses can improve cache utilization for supported workloads.
    /// </summary>
    public OpenAIProviderApiMode ApiMode { get; set; } = OpenAIProviderApiMode.Responses;

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
