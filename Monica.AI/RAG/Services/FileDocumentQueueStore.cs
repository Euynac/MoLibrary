using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.Modules;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.Tool.Extensions;

namespace Monica.AI.RAG.Services;

/// <summary>
/// File-backed implementation of <see cref="IDocumentQueueStore"/>.
/// Persists queue entries across service restarts.
/// </summary>
public sealed class FileDocumentQueueStore(
    IOptions<ModuleRAGOption> options,
    ILogger<FileDocumentQueueStore> logger) : IDocumentQueueStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath =
        GeneralExtensions.GetRelativePathInRunningPath(options.Value.DocumentQueueStoreFilePath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<DocumentQueueItem>> GetQueueAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await ReadAsync(ct);
            return items
                .Where(item => string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddAsync(DocumentQueueItem item, CancellationToken ct = default)
    {
        await UpsertAsync(item, ct);
    }

    public async Task UpdateAsync(DocumentQueueItem item, CancellationToken ct = default)
    {
        await UpsertAsync(item, ct);
    }

    public async Task RemoveAsync(string knowledgeBaseId, string documentId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await ReadAsync(ct);
            var removed = items.RemoveAll(item =>
                string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Id, documentId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteAsync(items, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DocumentQueueItem?> GetByIdAsync(
        string knowledgeBaseId,
        string documentId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await ReadAsync(ct);
            return items.FirstOrDefault(item =>
                string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Id, documentId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task UpsertAsync(DocumentQueueItem item, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await ReadAsync(ct);
            var index = items.FindIndex(existing =>
                string.Equals(existing.KnowledgeBaseId, item.KnowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                items[index] = item;
            }
            else
            {
                items.Add(item);
            }

            await WriteAsync(items, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<DocumentQueueItem>> ReadAsync(CancellationToken ct)
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

        return JsonSerializer.Deserialize<List<DocumentQueueItem>>(json, JsonOptions) ?? [];
    }

    private async Task WriteAsync(List<DocumentQueueItem> items, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(items, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        logger.LogDebug("Saved document queue to {Path}", _filePath);
    }
}
