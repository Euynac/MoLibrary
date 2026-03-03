using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Resolves chunkers and manages persisted extension routing.
/// </summary>
public sealed class ChunkerRegistry(
    IEnumerable<IDocumentChunker> chunkers,
    IChunkerRoutingStore routingStore,
    ILogger<ChunkerRegistry> logger)
{
    private readonly IReadOnlyList<IDocumentChunker> _chunkers = chunkers.ToList();

    private readonly Dictionary<string, IDocumentChunker> _chunkersById = chunkers
        .GroupBy(c => c.ChunkerId, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<IDocumentChunker>> _chunkersByExtension = chunkers
        .SelectMany(c => c.SupportedExtensions.Select(ext => (Extension: NormalizeExtension(ext), Chunker: c)))
        .GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            g => g.Key,
            g => g.Select(x => x.Chunker).DistinctBy(x => x.ChunkerId, StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ChunkerRoutingConfiguration _routing = new();
    private bool _initialized;

    public async Task<IReadOnlyList<ChunkerDescriptor>> GetChunkersAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        return _chunkers
            .Select(c => new ChunkerDescriptor
            {
                ChunkerId = c.ChunkerId,
                DisplayName = c.DisplayName,
                Description = c.Description,
                SupportedExtensions = c.SupportedExtensions
                    .Select(NormalizeExtension)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<ChunkerRouteDescriptor>> GetRoutesAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        return _chunkersByExtension
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var selected = ResolveDefaultChunkerIdUnsafe(x.Key, x.Value);
                return new ChunkerRouteDescriptor
                {
                    Extension = x.Key,
                    DefaultChunkerId = selected,
                    CandidateChunkerIds = x.Value
                        .Select(c => c.ChunkerId)
                        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            })
            .ToList();
    }

    public async Task<ChunkerManagementState> GetManagementStateAsync(CancellationToken ct = default)
    {
        var chunkersTask = GetChunkersAsync(ct);
        var routesTask = GetRoutesAsync(ct);
        await Task.WhenAll(chunkersTask, routesTask);

        return new ChunkerManagementState
        {
            Chunkers = chunkersTask.Result,
            Routes = routesTask.Result
        };
    }

    public async Task<IDocumentChunker> ResolveChunkerAsync(
        string documentPath,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var extension = NormalizeExtension(Path.GetExtension(documentPath));
        if (!_chunkersByExtension.TryGetValue(extension, out var candidates) || candidates.Count == 0)
        {
            throw new NotSupportedException($"No chunker registered for extension '{extension}'.");
        }

        var selectedChunkerId = ResolveDefaultChunkerIdUnsafe(extension, candidates);
        return _chunkersById[selectedChunkerId];
    }

    public async Task<string> GetCurrentDefaultChunkerIdAsync(
        string extension,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var normalizedExtension = NormalizeExtension(extension);
        if (!_chunkersByExtension.TryGetValue(normalizedExtension, out var candidates)
            || candidates.Count == 0)
        {
            throw new NotSupportedException($"No chunker registered for extension '{normalizedExtension}'.");
        }

        return ResolveDefaultChunkerIdUnsafe(normalizedExtension, candidates);
    }

    public async Task SetDefaultChunkerAsync(
        string extension,
        string chunkerId,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var normalizedExtension = NormalizeExtension(extension);
        if (!_chunkersByExtension.TryGetValue(normalizedExtension, out var candidates)
            || candidates.Count == 0)
        {
            throw new NotSupportedException($"No chunker registered for extension '{normalizedExtension}'.");
        }

        if (!_chunkersById.TryGetValue(chunkerId, out var chunker))
        {
            throw new KeyNotFoundException($"Chunker '{chunkerId}' not found.");
        }

        if (!candidates.Any(c => string.Equals(c.ChunkerId, chunker.ChunkerId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Chunker '{chunkerId}' does not support extension '{normalizedExtension}'.");
        }

        _routing.DefaultChunkers[normalizedExtension] = chunker.ChunkerId;
        await routingStore.SaveAsync(_routing, ct);
    }

    public bool TryGetChunker(string chunkerId, out IDocumentChunker? chunker)
    {
        return _chunkersById.TryGetValue(chunkerId, out chunker);
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            if (_chunkers.Count == 0)
            {
                throw new InvalidOperationException("No chunkers are registered.");
            }

            _routing = await routingStore.LoadAsync(ct);
            var currentDefaults = _routing.DefaultChunkers
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var entry in currentDefaults)
            {
                var extension = NormalizeExtension(entry.Key);
                var chunkerId = entry.Value?.Trim();

                if (string.IsNullOrWhiteSpace(chunkerId)
                    || !_chunkersById.TryGetValue(chunkerId, out var chunker)
                    || !_chunkersByExtension.TryGetValue(extension, out var candidates)
                    || !candidates.Any(c => string.Equals(c.ChunkerId, chunker.ChunkerId, StringComparison.OrdinalIgnoreCase)))
                {
                    changed = true;
                    continue;
                }

                normalized[extension] = chunker.ChunkerId;
            }

            if (changed || normalized.Count != currentDefaults.Count)
            {
                _routing = new ChunkerRoutingConfiguration
                {
                    DefaultChunkers = normalized
                };
                await routingStore.SaveAsync(_routing, ct);
                logger.LogInformation("Chunker routing configuration normalized and stale entries removed.");
            }
            else
            {
                _routing = new ChunkerRoutingConfiguration
                {
                    DefaultChunkers = normalized
                };
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string ResolveDefaultChunkerIdUnsafe(string extension, IReadOnlyList<IDocumentChunker> candidates)
    {
        if (_routing.DefaultChunkers.TryGetValue(extension, out var configuredChunkerId)
            && _chunkersById.TryGetValue(configuredChunkerId, out var configuredChunker)
            && candidates.Any(x => string.Equals(x.ChunkerId, configuredChunker.ChunkerId, StringComparison.OrdinalIgnoreCase)))
        {
            return configuredChunker.ChunkerId;
        }

        return candidates[0].ChunkerId;
    }

    private static string NormalizeExtension(string extension)
    {
        var normalized = extension.Trim();
        if (normalized.Length == 0)
        {
            return normalized;
        }

        return normalized.StartsWith(".", StringComparison.Ordinal)
            ? normalized.ToLowerInvariant()
            : $".{normalized.ToLowerInvariant()}";
    }
}
