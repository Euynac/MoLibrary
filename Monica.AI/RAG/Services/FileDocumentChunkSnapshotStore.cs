using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.Modules;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.Tool.Extensions;

namespace Monica.AI.RAG.Services;

/// <summary>
/// File-backed snapshot store for document chunks.
/// </summary>
public sealed class FileDocumentChunkSnapshotStore(
    IOptions<ModuleRAGOption> options,
    ILogger<FileDocumentChunkSnapshotStore> logger) : IDocumentChunkSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath =
        GeneralExtensions.GetRelativePathInRunningPath(options.Value.DocumentChunkSnapshotStoreFilePath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<DocumentChunkSnapshot>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var snapshots = await ReadAsync(ct);
            return snapshots;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<DocumentChunkSnapshot>> GetByKnowledgeBaseAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var snapshots = await ReadAsync(ct);
            return snapshots
                .Where(x => string.Equals(x.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DocumentChunkSnapshot?> GetAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var snapshots = await ReadAsync(ct);
            return snapshots.FirstOrDefault(x =>
                string.Equals(x.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(DocumentChunkSnapshot snapshot, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var snapshots = await ReadAsync(ct);
            var index = snapshots.FindIndex(x =>
                string.Equals(x.KnowledgeBaseId, snapshot.KnowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.DocumentPath, snapshot.DocumentPath, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                snapshots[index] = snapshot;
            }
            else
            {
                snapshots.Add(snapshot);
            }

            await WriteAsync(snapshots, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string knowledgeBaseId, string documentPath, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var snapshots = await ReadAsync(ct);
            var removed = snapshots.RemoveAll(x =>
                string.Equals(x.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                await WriteAsync(snapshots, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var snapshots = await ReadAsync(ct);
            var removed = snapshots.RemoveAll(x =>
                string.Equals(x.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteAsync(snapshots, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<DocumentChunkSnapshot>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<DocumentChunkSnapshot>>(json, JsonOptions) ?? [];
    }

    private async Task WriteAsync(List<DocumentChunkSnapshot> snapshots, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(snapshots, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        logger.LogDebug("Saved document chunk snapshots to {Path}", _filePath);
    }
}
