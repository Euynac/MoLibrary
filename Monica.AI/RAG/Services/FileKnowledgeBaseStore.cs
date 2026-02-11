using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.Modules;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.Tool.Extensions;

namespace Monica.AI.RAG.Services;

/// <summary>
/// File-based implementation of <see cref="IKnowledgeBaseStore"/>.
/// Persists knowledge base metadata as a JSON file on disk.
/// Suitable for development and lightweight deployments.
/// </summary>
public class FileKnowledgeBaseStore(
    IOptions<ModuleRAGOption> options,
    ILogger<FileKnowledgeBaseStore> logger) : IKnowledgeBaseStore
{
    private readonly string _filePath =
        GeneralExtensions.GetRelativePathInRunningPath(options.Value.KnowledgeBaseStoreFilePath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<KnowledgeBase>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await ReadFromFileAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<KnowledgeBase?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await ReadFromFileAsync(ct);
            return items.FirstOrDefault(kb => kb.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(KnowledgeBase knowledgeBase, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = (await ReadFromFileAsync(ct)).ToList();
            var existingIndex = items.FindIndex(kb => kb.Id == knowledgeBase.Id);

            if (existingIndex >= 0)
                items[existingIndex] = knowledgeBase;
            else
                items.Add(knowledgeBase);

            await WriteToFileAsync(items, ct);
            logger.LogDebug("Saved knowledge base '{Id}' to {Path}", knowledgeBase.Id, _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = (await ReadFromFileAsync(ct)).ToList();
            var removed = items.RemoveAll(kb => kb.Id == id);

            if (removed > 0)
            {
                await WriteToFileAsync(items, ct);
                logger.LogDebug("Deleted knowledge base '{Id}' from {Path}", id, _filePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<KnowledgeBase>> ReadFromFileAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<KnowledgeBase>>(json, JsonOptions) ?? [];
    }

    private async Task WriteToFileAsync(List<KnowledgeBase> items, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(items, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
