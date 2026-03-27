namespace Monica.AI.Models;

/// <summary>
/// AI model metadata information base class
/// </summary>
public abstract class AIModelInfo
{
    /// <summary>
    /// Model name
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Model description
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Large language model metadata information
/// </summary>
public class LLMModelInfo : AIModelInfo
{
    /// <summary>
    /// Whether to support image input
    /// </summary>
    public bool SupportsImage { get; init; }

    /// <summary>
    /// Whether it supports deep thinking/reasoning
    /// </summary>
    public bool SupportsReasoning { get; init; }

    /// <summary>
    /// Context window size (optional)
    /// </summary>
    public int? ContextWindow { get; init; }

    /// <summary>
    /// Maximum number of output tokens (optional)
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Enter fee (USD/1M tokens)
    /// </summary>
    public decimal InputCostPerMillionTokens { get; init; }

    /// <summary>
    /// Output fee (USD/1M tokens)
    /// </summary>
    public decimal OutputCostPerMillionTokens { get; init; }

    /// <summary>
    /// Cache hit input fee (USD/1M tokens)
    /// </summary>
    public decimal CachedInputCostPerMillionTokens { get; init; }
}

/// <summary>
/// Image model metadata information
/// </summary>
public class ImageModelInfo : AIModelInfo
{
    /// <summary>
    /// Maximum image size (optional, e.g. 1024 for 1024x1024)
    /// </summary>
    public int? MaxImageSize { get; init; }

    /// <summary>
    /// Whether to support editing/transformation
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
/// Text-to-speech model metadata information
/// </summary>
public class TextToSpeechModelInfo : AIModelInfo
{
    /// <summary>
    /// Available sounds list
    /// </summary>
    public IReadOnlyList<string>? Voices { get; init; }

    /// <summary>
    /// Whether to support streaming output
    /// </summary>
    public bool SupportsStreaming { get; init; }
}
