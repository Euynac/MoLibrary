using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Coordinates document and knowledge-base index state transitions.
/// </summary>
internal sealed class RAGIndexStateCoordinator(IDocumentIndexStateStore indexStateStore)
{
    private const int MaxInProgressPercentage = 99;

    public async Task<KnowledgeBaseModel> GetKnowledgeBaseRequiredAsync(string knowledgeBaseId, CancellationToken ct)
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

    /// <summary>
    /// Recovers persisted "Indexing" records that are not active at runtime.
    /// This is an explicit recovery path to avoid zombie indexing states.
    /// </summary>
    public async Task<int> ConvergeInactiveIndexingDocumentsAsync(
        string knowledgeBaseId,
        Func<string, string, bool> isRuntimeIndexingActive,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(isRuntimeIndexingActive);

        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        var staleCandidates = states
            .Where(state =>
                state.Status == DocumentStatus.Indexing
                && !isRuntimeIndexingActive(state.KnowledgeBaseId, state.DocumentPath))
            .ToList();

        var recovered = 0;
        foreach (var stale in staleCandidates)
        {
            var latest = await indexStateStore.GetDocumentStateAsync(stale.KnowledgeBaseId, stale.DocumentPath, ct);
            if (latest is null || latest.Status != DocumentStatus.Indexing)
            {
                continue;
            }

            if (isRuntimeIndexingActive(latest.KnowledgeBaseId, latest.DocumentPath))
            {
                continue;
            }

            await SetPendingStateAsync(latest, resetChunkMetadata: true, ct);
            recovered++;
        }

        return recovered;
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
        // Explicit indexing-start entry point. This is the only path that can move terminal states back to Indexing.
        var latest = await LoadCurrentStateOrFallbackAsync(state, ct);
        latest.DocumentName = documentName;
        latest.Status = DocumentStatus.Indexing;
        latest.Progress = 0;
        latest.ChunkCount = 0;
        latest.IndexedAt = null;
        latest.ErrorMessage = null;
        latest.ChunkerId = null;
        latest.SourceKind = NormalizeSourceKind(sourceKind);
        latest.SourceGroupKey = sourceGroupKey;
        latest.UpdatedAt = DateTimeOffset.UtcNow;

        await indexStateStore.UpsertDocumentStateAsync(latest, ct);
    }

    public async Task SetDoneStateAsync(
        DocumentIndexState state,
        int chunkCount,
        string chunkerId,
        CancellationToken ct)
    {
        var latest = await LoadCurrentStateOrFallbackAsync(state, ct);
        if (latest.Status is not (DocumentStatus.Indexing or DocumentStatus.Done))
        {
            return;
        }

        latest.Status = DocumentStatus.Done;
        latest.ChunkCount = Math.Max(0, chunkCount);
        latest.Progress = 100;
        latest.IndexedAt = DateTimeOffset.UtcNow;
        latest.ErrorMessage = null;
        latest.ChunkerId = chunkerId;
        latest.UpdatedAt = DateTimeOffset.UtcNow;

        await indexStateStore.UpsertDocumentStateAsync(latest, ct);
    }

    public async Task SetPendingStateAsync(
        DocumentIndexState state,
        bool resetChunkMetadata,
        CancellationToken ct)
    {
        var latest = await LoadCurrentStateOrFallbackAsync(state, ct);
        latest.Status = DocumentStatus.Pending;
        latest.Progress = 0;
        latest.ErrorMessage = null;

        if (resetChunkMetadata)
        {
            latest.ChunkCount = 0;
            latest.IndexedAt = null;
            latest.ChunkerId = null;
        }

        latest.UpdatedAt = DateTimeOffset.UtcNow;
        await indexStateStore.UpsertDocumentStateAsync(latest, ct);
    }

    public async Task SetIndexingProgressAsync(
        DocumentIndexState state,
        int progress,
        int chunkCount,
        CancellationToken ct)
    {
        var latest = await indexStateStore.GetDocumentStateAsync(state.KnowledgeBaseId, state.DocumentPath, ct);
        if (latest is null || latest.Status != DocumentStatus.Indexing)
        {
            // Progress writes are only valid while the persisted state is Indexing.
            return;
        }

        var normalizedProgress = Math.Clamp(progress, 0, MaxInProgressPercentage);
        var normalizedChunkCount = Math.Max(0, chunkCount);
        if (latest.Progress == normalizedProgress
            && latest.ChunkCount == normalizedChunkCount
            && latest.ErrorMessage is null)
        {
            return;
        }

        latest.Progress = normalizedProgress;
        latest.ChunkCount = normalizedChunkCount;
        latest.ErrorMessage = null;
        latest.UpdatedAt = DateTimeOffset.UtcNow;

        await indexStateStore.UpsertDocumentStateAsync(latest, ct);
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

        if (state.Status == DocumentStatus.Done)
        {
            // Done is terminal until an explicit indexing restart.
            return;
        }

        state.Status = DocumentStatus.Error;
        state.Progress = 100;
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

    /// <summary>
    /// Resets indexed document-state records to pending after their vector data has been removed.
    /// </summary>
    public async Task<int> ResetIndexedDocumentStatesToPendingAsync(string knowledgeBaseId, CancellationToken ct)
    {
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        var resetCount = 0;
        foreach (var state in states.Where(static state => state.Status == DocumentStatus.Done || state.ChunkCount > 0))
        {
            state.Status = DocumentStatus.Pending;
            state.ChunkCount = 0;
            state.Progress = 0;
            state.IndexedAt = null;
            state.ErrorMessage = null;
            state.ChunkerId = null;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await indexStateStore.UpsertDocumentStateAsync(state, ct);
            resetCount++;
        }

        return resetCount;
    }

    public async Task RefreshKnowledgeBaseStatsAsync(KnowledgeBaseModel kb, CancellationToken ct)
    {
        var indexedStates = await indexStateStore.GetDocumentStatesAsync(kb.Id, ct);
        var documentCount = indexedStates.Count;
        var chunkCount = indexedStates.Sum(state => Math.Max(0, state.ChunkCount));

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

    private async Task<DocumentIndexState> LoadCurrentStateOrFallbackAsync(
        DocumentIndexState state,
        CancellationToken ct)
    {
        return await indexStateStore.GetDocumentStateAsync(state.KnowledgeBaseId, state.DocumentPath, ct)
               ?? state;
    }

    private static DocumentQueueItem MapToQueueItem(DocumentIndexState state)
    {
        var name = string.IsNullOrWhiteSpace(state.DocumentName)
            ? ResolveDocumentName(state.DocumentPath, state.DocumentPath)
            : state.DocumentName;

        // Normalize legacy low-level vector-store failures before they reach queue consumers.
        var errorMessage = state.Status == DocumentStatus.Error
            ? RAGFailureTranslator.NormalizeDocumentErrorMessage(state.ErrorMessage)
            : state.ErrorMessage;

        return new DocumentQueueItem
        {
            Id = state.DocumentPath,
            Name = name,
            KnowledgeBaseId = state.KnowledgeBaseId,
            Status = state.Status,
            ChunkCount = state.ChunkCount,
            Progress = state.Progress,
            IndexedAt = state.IndexedAt,
            ErrorMessage = errorMessage,
            SourceKind = state.SourceKind,
            SourceGroupKey = state.SourceGroupKey
        };
    }
}
