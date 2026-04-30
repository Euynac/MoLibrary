namespace Monica.AI.RAG.Models;

/// <summary>
/// Runtime diagnostic metadata for one configured embedding model.
/// </summary>
public sealed record EmbeddingModelDiagnosticInfo
{
    /// <summary>
    /// Provider identifier that owns the embedding model.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Provider display name.
    /// </summary>
    public required string ProviderDisplayName { get; init; }

    /// <summary>
    /// Provider status text.
    /// </summary>
    public required string ProviderStatus { get; init; }

    /// <summary>
    /// Whether the provider is valid and can be used by indexing.
    /// </summary>
    public bool IsProviderValid { get; init; }

    /// <summary>
    /// Embedding model name.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Configured embedding vector dimensions, when known.
    /// </summary>
    public int? Dimensions { get; init; }

    /// <summary>
    /// Optional provider/model description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Provider configuration errors that currently block usage.
    /// </summary>
    public IReadOnlyList<string> ConfigurationErrors { get; init; } = [];

    /// <summary>
    /// Stable key used by UI rows and test-result maps.
    /// </summary>
    public string ModelKey => EmbeddingModelOption.ToModelKey(ProviderId, ModelName);
}
