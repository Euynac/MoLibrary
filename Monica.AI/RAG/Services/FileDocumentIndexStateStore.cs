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
/// File-backed unified state store for knowledge bases and document index states.
/// </summary>
public sealed class FileDocumentIndexStateStore(
    IOptions<ModuleRAGOption> options,
    ILogger<FileDocumentIndexStateStore> logger) : IDocumentIndexStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath =
        RuntimePathHelper.GetRelativePathInRunningPath(options.Value.DocumentIndexStateStoreFilePath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<KnowledgeBase>> GetKnowledgeBasesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            return payload.KnowledgeBases.Select(CloneKnowledgeBase).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<KnowledgeBase?> GetKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            var kb = payload.KnowledgeBases.FirstOrDefault(item =>
                string.Equals(item.Id, knowledgeBaseId, StringComparison.OrdinalIgnoreCase));
            return kb is null ? null : CloneKnowledgeBase(kb);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpsertKnowledgeBaseAsync(KnowledgeBase knowledgeBase, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            var index = payload.KnowledgeBases.FindIndex(item =>
                string.Equals(item.Id, knowledgeBase.Id, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                payload.KnowledgeBases[index] = CloneKnowledgeBase(knowledgeBase);
            }
            else
            {
                payload.KnowledgeBases.Add(CloneKnowledgeBase(knowledgeBase));
            }

            await WriteAsync(payload, ct);
            logger.LogDebug("Upserted knowledge base '{KbId}' into {Path}", knowledgeBase.Id, _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            var removed = payload.KnowledgeBases.RemoveAll(item =>
                string.Equals(item.Id, knowledgeBaseId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return;
            }

            payload.DocumentStates.RemoveAll(item =>
                string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase));
            await WriteAsync(payload, ct);
            logger.LogDebug("Deleted knowledge base '{KbId}' from {Path}", knowledgeBaseId, _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<DocumentIndexState>> GetDocumentStatesAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            return payload.DocumentStates
                .Where(item => string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase))
                .Select(CloneDocumentState)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<DocumentIndexState>> GetAllDocumentStatesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            return payload.DocumentStates.Select(CloneDocumentState).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DocumentIndexState?> GetDocumentStateAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            var state = payload.DocumentStates.FirstOrDefault(item =>
                string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase));
            return state is null ? null : CloneDocumentState(state);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpsertDocumentStateAsync(DocumentIndexState state, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            var index = payload.DocumentStates.FindIndex(item =>
                string.Equals(item.KnowledgeBaseId, state.KnowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.DocumentPath, state.DocumentPath, StringComparison.OrdinalIgnoreCase));

            var existing = index >= 0 ? payload.DocumentStates[index] : null;
            var nextVersion = existing is null
                ? Math.Max(1, state.RowVersion)
                : Math.Max(existing.RowVersion + 1, state.RowVersion + 1);

            var normalized = CloneDocumentState(state);
            normalized.RowVersion = nextVersion;
            if (normalized.UpdatedAt == default)
            {
                normalized.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (index >= 0)
            {
                payload.DocumentStates[index] = normalized;
            }
            else
            {
                payload.DocumentStates.Add(normalized);
            }

            await WriteAsync(payload, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteDocumentStateAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            var removed = payload.DocumentStates.RemoveAll(item =>
                string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteAsync(payload, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteKnowledgeBaseDocumentStatesAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var payload = await ReadAsync(ct);
            var removed = payload.DocumentStates.RemoveAll(item =>
                string.Equals(item.KnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteAsync(payload, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<DocumentIndexStateStorePayload> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return new DocumentIndexStateStorePayload();
        }

        var json = await File.ReadAllTextAsync(_filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new DocumentIndexStateStorePayload();
        }

        return JsonSerializer.Deserialize<DocumentIndexStateStorePayload>(json, JsonOptions)
               ?? new DocumentIndexStateStorePayload();
    }

    private async Task WriteAsync(DocumentIndexStateStorePayload payload, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    private static KnowledgeBase CloneKnowledgeBase(KnowledgeBase kb)
        => kb with { };

    private static DocumentIndexState CloneDocumentState(DocumentIndexState state)
    {
        return new DocumentIndexState
        {
            KnowledgeBaseId = state.KnowledgeBaseId,
            DocumentPath = state.DocumentPath,
            DocumentName = state.DocumentName,
            Status = state.Status,
            ChunkCount = state.ChunkCount,
            Progress = state.Progress,
            IndexedAt = state.IndexedAt,
            ErrorMessage = state.ErrorMessage,
            UpdatedAt = state.UpdatedAt,
            RowVersion = state.RowVersion,
            ChunkerId = state.ChunkerId,
            SourceKind = state.SourceKind,
            SourceGroupKey = state.SourceGroupKey
        };
    }

    private sealed class DocumentIndexStateStorePayload
    {
        public List<KnowledgeBase> KnowledgeBases { get; init; } = [];

        public List<DocumentIndexState> DocumentStates { get; init; } = [];
    }
}
