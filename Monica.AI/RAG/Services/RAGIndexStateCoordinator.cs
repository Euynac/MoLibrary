using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Coordinates document and knowledge-base index state transitions.
/// </summary>
public sealed class RAGIndexStateCoordinator(IDocumentIndexStateStore indexStateStore)
{
    public async Task<KnowledgeBase> GetKnowledgeBaseRequiredAsync(string knowledgeBaseId, CancellationToken ct)
    {
        return await indexStateStore.GetKnowledgeBaseAsync(knowledgeBaseId, ct)
               ?? throw new KeyNotFoundException($"Knowledge base '{knowledgeBaseId}' not found.");
    }

    public Task<DocumentIndexState?> GetDocumentStateAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct)
        => indexStateStore.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct);

    public async Task<DocumentIndexState> GetDocumentStateRequiredAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct)
    {
        return await indexStateStore.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct)
               ?? throw new KeyNotFoundException(
                   $"Document '{documentPath}' not found in knowledge base '{knowledgeBaseId}'.");
    }

    public async Task<IReadOnlyList<DocumentQueueItem>> GetDocumentQueueAsync(string knowledgeBaseId, CancellationToken ct)
    {
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        return states
            .Select(MapToQueueItem)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> AddDocumentsToQueueAsync(
        string knowledgeBaseId,
        IEnumerable<string> documentPaths,
        string sourceKind,
        string? sourceGroupKey,
        CancellationToken ct)
    {
        var addedCount = 0;
        foreach (var documentPath in documentPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(path => path.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var existing = await indexStateStore.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct);
            if (existing is not null)
            {
                continue;
            }

            await indexStateStore.UpsertDocumentStateAsync(
                new DocumentIndexState
                {
                    KnowledgeBaseId = knowledgeBaseId,
                    DocumentPath = documentPath,
                    DocumentName = ResolveDocumentName(documentPath, documentPath),
                    Status = DocumentStatus.Pending,
                    ChunkCount = 0,
                    Progress = 0,
                    IndexedAt = null,
                    ErrorMessage = null,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    ChunkerId = null,
                    SourceKind = NormalizeSourceKind(sourceKind),
                    SourceGroupKey = sourceGroupKey
                },
                ct);

            addedCount++;
        }

        return addedCount;
    }

    public async Task SetIndexingStateAsync(
        DocumentIndexState state,
        string documentName,
        string sourceKind,
        string? sourceGroupKey,
        CancellationToken ct)
    {
        state.DocumentName = documentName;
        state.Status = DocumentStatus.Indexing;
        state.Progress = 0;
        state.ChunkCount = 0;
        state.IndexedAt = null;
        state.ErrorMessage = null;
        state.ChunkerId = null;
        state.SourceKind = NormalizeSourceKind(sourceKind);
        state.SourceGroupKey = sourceGroupKey;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        await indexStateStore.UpsertDocumentStateAsync(state, ct);
    }

    public async Task SetDoneStateAsync(
        DocumentIndexState state,
        int chunkCount,
        string chunkerId,
        CancellationToken ct)
    {
        state.Status = DocumentStatus.Done;
        state.ChunkCount = Math.Max(0, chunkCount);
        state.Progress = 100;
        state.IndexedAt = DateTimeOffset.UtcNow;
        state.ErrorMessage = null;
        state.ChunkerId = chunkerId;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        await indexStateStore.UpsertDocumentStateAsync(state, ct);
    }

    public async Task SetPendingStateAsync(
        DocumentIndexState state,
        bool resetChunkMetadata,
        CancellationToken ct)
    {
        state.Status = DocumentStatus.Pending;
        state.Progress = 0;
        state.ErrorMessage = null;

        if (resetChunkMetadata)
        {
            state.ChunkCount = 0;
            state.IndexedAt = null;
            state.ChunkerId = null;
        }

        state.UpdatedAt = DateTimeOffset.UtcNow;
        await indexStateStore.UpsertDocumentStateAsync(state, ct);
    }

    public async Task SetIndexingProgressAsync(
        DocumentIndexState state,
        int progress,
        int chunkCount,
        CancellationToken ct)
    {
        state.Status = DocumentStatus.Indexing;
        state.Progress = Math.Clamp(progress, 0, 100);
        state.ChunkCount = Math.Max(0, chunkCount);
        state.ErrorMessage = null;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        await indexStateStore.UpsertDocumentStateAsync(state, ct);
    }

    public async Task MarkDocumentFailedAsync(
        string knowledgeBaseId,
        string documentPath,
        string errorMessage,
        CancellationToken ct)
    {
        var state = await indexStateStore.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct)
                    ?? new DocumentIndexState
                    {
                        KnowledgeBaseId = knowledgeBaseId,
                        DocumentPath = documentPath,
                        DocumentName = ResolveDocumentName(documentPath, documentPath),
                        Status = DocumentStatus.Error,
                        SourceKind = KnowledgeDocumentSourceKinds.Unknown,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

        state.Status = DocumentStatus.Error;
        state.ErrorMessage = errorMessage;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        await indexStateStore.UpsertDocumentStateAsync(state, ct);
    }

    public Task DeleteDocumentStateAsync(string knowledgeBaseId, string documentPath, CancellationToken ct)
        => indexStateStore.DeleteDocumentStateAsync(knowledgeBaseId, documentPath, ct);

    public async Task ResetKnowledgeBaseDocumentStatesForReindexAsync(string knowledgeBaseId, CancellationToken ct)
    {
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        foreach (var state in states)
        {
            state.Status = DocumentStatus.Pending;
            state.ChunkCount = 0;
            state.Progress = 0;
            state.IndexedAt = null;
            state.ErrorMessage = null;
            state.ChunkerId = null;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await indexStateStore.UpsertDocumentStateAsync(state, ct);
        }
    }

    public async Task RefreshKnowledgeBaseStatsAsync(KnowledgeBase kb, CancellationToken ct)
    {
        var indexedStates = await indexStateStore.GetDocumentStatesAsync(kb.Id, ct);
        var doneStates = indexedStates.Where(state => state.Status == DocumentStatus.Done).ToList();

        var documentCount = doneStates.Count;
        var chunkCount = doneStates.Sum(state => state.ChunkCount);

        if (kb.DocumentCount == documentCount && kb.ChunkCount == chunkCount)
        {
            return;
        }

        kb.DocumentCount = documentCount;
        kb.ChunkCount = chunkCount;
        await indexStateStore.UpsertKnowledgeBaseAsync(kb, ct);
    }

    public static string ResolveDocumentName(string documentTitle, string documentPath)
    {
        if (!string.IsNullOrWhiteSpace(documentTitle))
        {
            return documentTitle.Trim();
        }

        var fileName = Path.GetFileName(documentPath);
        return string.IsNullOrWhiteSpace(fileName) ? documentPath : fileName;
    }

    public static string NormalizeSourceKind(string? sourceKind)
    {
        return string.IsNullOrWhiteSpace(sourceKind)
            ? KnowledgeDocumentSourceKinds.Unknown
            : sourceKind.Trim().ToLowerInvariant();
    }

    private static DocumentQueueItem MapToQueueItem(DocumentIndexState state)
    {
        var name = string.IsNullOrWhiteSpace(state.DocumentName)
            ? ResolveDocumentName(state.DocumentPath, state.DocumentPath)
            : state.DocumentName;

        return new DocumentQueueItem
        {
            Id = state.DocumentPath,
            Name = name,
            KnowledgeBaseId = state.KnowledgeBaseId,
            Status = state.Status,
            ChunkCount = state.ChunkCount,
            Progress = state.Progress,
            IndexedAt = state.IndexedAt,
            ErrorMessage = state.ErrorMessage
        };
    }
}
