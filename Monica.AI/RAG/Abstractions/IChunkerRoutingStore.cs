using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Persists default chunker routing per file extension.
/// </summary>
public interface IChunkerRoutingStore
{
    Task<ChunkerRoutingConfiguration> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(ChunkerRoutingConfiguration configuration, CancellationToken ct = default);
}
