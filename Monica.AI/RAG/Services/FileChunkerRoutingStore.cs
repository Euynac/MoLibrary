using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.Tool.Extensions;
using Monica.Tool.Runtime;

namespace Monica.AI.RAG.Services;

/// <summary>
/// File-backed chunker routing store.
/// </summary>
public sealed class FileChunkerRoutingStore(
    IOptions<ModuleRAGOption> options,
    ILogger<FileChunkerRoutingStore> logger) : IChunkerRoutingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath =
        RuntimePathHelper.GetRelativePathInRunningPath(options.Value.ChunkerRoutingStoreFilePath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<ChunkerRoutingConfiguration> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ChunkerRoutingConfiguration();
            }

            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ChunkerRoutingConfiguration();
            }

            return JsonSerializer.Deserialize<ChunkerRoutingConfiguration>(json, JsonOptions)
                   ?? new ChunkerRoutingConfiguration();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ChunkerRoutingConfiguration configuration, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            var normalized = new ChunkerRoutingConfiguration
            {
                DefaultChunkers = new Dictionary<string, string>(
                    configuration.DefaultChunkers
                        .Where(x => !string.IsNullOrWhiteSpace(x.Key)
                                    && !string.IsNullOrWhiteSpace(x.Value))
                        .Select(x => new KeyValuePair<string, string>(
                            NormalizeExtension(x.Key),
                            x.Value.Trim())),
                    StringComparer.OrdinalIgnoreCase)
            };

            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);

            logger.LogDebug("Saved chunker routing to {Path}", _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string NormalizeExtension(string extension)
    {
        var normalized = extension.Trim();
        if (normalized.Length == 0)
        {
            return normalized;
        }

        return normalized.StartsWith(".", StringComparison.Ordinal) ? normalized : $".{normalized}";
    }
}
