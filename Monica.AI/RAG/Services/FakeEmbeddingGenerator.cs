using Microsoft.Extensions.AI;

namespace Monica.AI.RAG.Services;

/// <summary>
/// A fake embedding generator for testing and development.
/// Generates random embedding vectors without requiring any external API or model.
/// </summary>
/// <remarks>
/// This generator produces random vectors, meaning semantic similarity search
/// will return arbitrary results. Use this for:
/// - Unit and integration testing
/// - CI/CD pipelines
/// - Prototyping without API costs
/// - Validating RAG infrastructure without a real embedding model
/// </remarks>
public sealed class FakeEmbeddingGenerator(int dimensions = 384)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly EmbeddingGeneratorMetadata _metadata =
        new("FakeEmbeddingGenerator", new Uri("http://localhost"), "fake-embedding-model");

    /// <summary>
    /// The number of dimensions for generated embedding vectors.
    /// </summary>
    public int Dimensions { get; } = dimensions;

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveDimensions = options?.Dimensions ?? Dimensions;

        var embeddings = values.Select(value =>
            new Embedding<float>(
                Enumerable.Range(0, effectiveDimensions)
                    .Select(_ => Random.Shared.NextSingle())
                    .ToArray()));

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([.. embeddings]));
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey) =>
        serviceKey is not null ? null
        : serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata
        : serviceType?.IsInstanceOfType(this) is true ? this
        : null;

    /// <inheritdoc />
    void IDisposable.Dispose() { }
}
