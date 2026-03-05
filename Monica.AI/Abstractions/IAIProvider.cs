using Microsoft.Extensions.AI;
using Monica.AI.Models;

namespace Monica.AI.Abstractions;

/// <summary>
/// AI Provider abstraction interface defining basic operations for AI service providers.
/// </summary>
public interface IAIProvider : IDisposable
{
    /// <summary>
    /// Provider unique identifier.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Provider type (for example: OpenAI, Anthropic).
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Provider display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Provider metadata information.
    /// </summary>
    AIProviderInfo Info { get; }

    /// <summary>
    /// Gets an IChatClient instance for the specified model.
    /// </summary>
    IChatClient GetChatClient(string? modelName = null);

    /// <summary>
    /// Gets an embedding generator for the specified model.
    /// If modelName is null, uses the first EmbeddingModelInfo from the provider's resolved models.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown when the provider does not support embedding generation
    /// or no embedding models are configured in SupportedModels.
    /// </exception>
    IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(
        string? modelName = null);

    /// <summary>
    /// Tests whether the connection is working.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection test fails.
    /// </exception>
    Task TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the list of available models.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches the list of models actually available from the remote provider API.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown when the provider does not support remote model listing.
    /// </exception>
    Task<IReadOnlyList<AIRemoteModelInfo>> FetchRemoteModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Whether this provider supports fetching remote model lists.
    /// </summary>
    bool SupportsRemoteModelListing => true;

    /// <summary>
    /// Updates the provider's default system prompt.
    /// </summary>
    void UpdateSystemPrompt(string? systemPrompt);
}
