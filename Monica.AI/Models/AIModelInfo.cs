namespace Monica.AI.Models;

/// <summary>
/// Base class for AI model metadata.
/// </summary>
public abstract class AIModelInfo
{
    /// <summary>
    /// Model name.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Model description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Metadata for a large language model.
/// </summary>
public class LLMModelInfo : AIModelInfo
{
    /// <summary>
    /// Indicates whether image input is supported.
    /// </summary>
    public bool SupportsImage { get; init; }

    /// <summary>
    /// Indicates whether reasoning is supported.
    /// </summary>
    public bool SupportsReasoning { get; init; }

    /// <summary>
    /// Context window size, if available.
    /// </summary>
    public int? ContextWindow { get; init; }

    /// <summary>
    /// Maximum number of output tokens, if available.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Input cost in USD per 1M tokens.
    /// </summary>
    public decimal InputCostPerMillionTokens { get; init; }

    /// <summary>
    /// Output cost in USD per 1M tokens.
    /// </summary>
    public decimal OutputCostPerMillionTokens { get; init; }

    /// <summary>
    /// Cached input cost in USD per 1M tokens.
    /// </summary>
    public decimal CachedInputCostPerMillionTokens { get; init; }
}

/// <summary>
/// Metadata for an image model.
/// </summary>
public class ImageModelInfo : AIModelInfo
{
    /// <summary>
    /// Maximum image size, if available (for example, 1024 for 1024x1024).
    /// </summary>
    public int? MaxImageSize { get; init; }

    /// <summary>
    /// Indicates whether editing or transformation is supported.
    /// </summary>
    public bool SupportsEditing { get; init; }
}

/// <summary>
/// Embedding model metadata.
/// </summary>
public class EmbeddingModelInfo : AIModelInfo
{
    /// <summary>
    /// Vector dimensions produced by this embedding model.
    /// Null if unknown — use the probe feature to detect at runtime.
    /// </summary>
    public int? Dimensions { get; set; }

    /// <summary>
    /// Maximum input tokens per embedding request.
    /// </summary>
    public int? MaxInputTokens { get; init; }

    /// <summary>
    /// Cost per 1M tokens (USD).
    /// </summary>
    public decimal CostPerMillionTokens { get; init; }
}

/// <summary>
/// Metadata for a text-to-speech model.
/// </summary>
public class TextToSpeechModelInfo : AIModelInfo
{
    /// <summary>
    /// List of available voices.
    /// </summary>
    public IReadOnlyList<string>? Voices { get; init; }

    /// <summary>
    /// Indicates whether streaming output is supported.
    /// </summary>
    public bool SupportsStreaming { get; init; }
}
