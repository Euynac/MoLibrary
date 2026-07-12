using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Owns chunk previews, chunker diagnostics, and persisted extension routing changes.
/// </summary>
internal sealed class RAGChunkingService(
    IDocumentIndexStateStore indexStateStore,
    IKnowledgeDocumentSourceStore sourceStore,
    RAGIndexStateCoordinator indexStateCoordinator,
    RAGVectorCollectionCoordinator vectorCollections,
    ChunkerRegistry chunkerRegistry)
{
    /// <summary>Builds a chunk view from persisted source content.</summary>
    public async Task<DocumentChunkView?> GetDocumentChunkViewAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStateCoordinator.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct);
        if (state is null)
        {
            return null;
        }

        var content = await sourceStore.GetContentAsync(knowledgeBaseId, documentPath, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var chunker = await ResolveChunkerForViewAsync(state, ct);
        return BuildChunkView(
            content,
            chunker.ChunkDocument(content, state.DocumentPath, state.DocumentName),
            chunker.ChunkerId,
            isPreview: false,
            state.DocumentPath,
            state.DocumentName,
            state.SourceKind,
            state.SourceGroupKey);
    }

    /// <summary>Builds a non-persisted chunk preview.</summary>
    public async Task<DocumentChunkView> BuildPreviewAsync(
        string knowledgeBaseId,
        string documentPath,
        string documentTitle,
        string content,
        string? sourceKind = null,
        string? sourceGroupKey = null,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Cannot build chunk preview from empty content.");
        }

        var chunker = await chunkerRegistry.ResolveChunkerAsync(documentPath, ct);
        return BuildChunkView(
            content,
            chunker.ChunkDocument(content, documentPath, documentTitle),
            chunker.ChunkerId,
            isPreview: true,
            documentPath,
            documentTitle,
            sourceKind,
            sourceGroupKey);
    }

    /// <summary>Returns chunker registrations and routing state.</summary>
    public Task<ChunkerManagementState> GetManagementStateAsync(CancellationToken ct = default)
        => chunkerRegistry.GetManagementStateAsync(ct);

    /// <summary>Previews the documents affected by a routing change.</summary>
    public async Task<ChunkerRoutingChangePreview> PreviewRoutingChangeAsync(
        string extension,
        string targetChunkerId,
        CancellationToken ct = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var route = (await chunkerRegistry.GetRoutesAsync(ct)).FirstOrDefault(candidate =>
                        string.Equals(candidate.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase))
                    ?? throw new NotSupportedException(
                        $"No chunker route is available for extension '{normalizedExtension}'.");
        if (!route.CandidateChunkerIds.Contains(targetChunkerId, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Chunker '{targetChunkerId}' does not support extension '{normalizedExtension}'.");
        }

        var affected = await GetAffectedDocumentsAsync(normalizedExtension, ct);
        return new ChunkerRoutingChangePreview
        {
            Extension = normalizedExtension,
            CurrentChunkerId = route.DefaultChunkerId,
            TargetChunkerId = targetChunkerId,
            AffectedDocumentCount = affected.Count,
            AffectedKnowledgeBaseIds = affected
                .Select(static item => item.KnowledgeBase.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    /// <summary>Applies a routing change and invalidates vectors created by the old chunker.</summary>
    public async Task<ChunkerRoutingApplyResult> ApplyRoutingChangeAsync(
        string extension,
        string targetChunkerId,
        CancellationToken ct = default)
    {
        var preview = await PreviewRoutingChangeAsync(extension, targetChunkerId, ct);
        if (string.Equals(preview.CurrentChunkerId, preview.TargetChunkerId, StringComparison.OrdinalIgnoreCase))
        {
            return new ChunkerRoutingApplyResult
            {
                Extension = preview.Extension,
                TargetChunkerId = preview.TargetChunkerId,
                MarkedPendingDocumentCount = 0
            };
        }

        var affected = await GetAffectedDocumentsAsync(preview.Extension, ct);
        foreach (var item in affected)
        {
            _ = await vectorCollections.RemoveIndexedDocumentDataAsync(item.KnowledgeBase, item.State, ct);
            await indexStateCoordinator.SetPendingStateAsync(item.State, resetChunkMetadata: true, ct);
        }

        foreach (var knowledgeBase in affected
                     .Select(static item => item.KnowledgeBase)
                     .DistinctBy(static knowledgeBase => knowledgeBase.Id, StringComparer.OrdinalIgnoreCase))
        {
            await indexStateCoordinator.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);
        }

        await chunkerRegistry.SetDefaultChunkerAsync(preview.Extension, preview.TargetChunkerId, ct);
        return new ChunkerRoutingApplyResult
        {
            Extension = preview.Extension,
            TargetChunkerId = preview.TargetChunkerId,
            MarkedPendingDocumentCount = affected.Count
        };
    }

    /// <summary>Runs one chunker against supplied text without persisting changes.</summary>
    public Task<ChunkerTestResult> TestAsync(
        string chunkerId,
        string documentName,
        string content,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkerId);
        if (!chunkerRegistry.TryGetChunker(chunkerId, out var chunker) || chunker is null)
        {
            throw new KeyNotFoundException($"Chunker '{chunkerId}' was not found.");
        }

        var resolvedName = string.IsNullOrWhiteSpace(documentName) ? "sample.md" : documentName.Trim();
        var originalText = content ?? string.Empty;
        var chunks = chunker.ChunkDocument(originalText, resolvedName, Path.GetFileNameWithoutExtension(resolvedName));
        return Task.FromResult(new ChunkerTestResult
        {
            ChunkerId = chunker.ChunkerId,
            DocumentName = resolvedName,
            OriginalText = originalText,
            Chunks = BuildHighlights(originalText, chunks)
        });
    }

    private async Task<List<AffectedDocument>> GetAffectedDocumentsAsync(string extension, CancellationToken ct)
    {
        var states = await indexStateStore.GetAllDocumentStatesAsync(ct);
        var knowledgeBases = (await indexStateStore.GetKnowledgeBasesAsync(ct))
            .ToDictionary(static item => item.Id, StringComparer.OrdinalIgnoreCase);

        return states
            .Where(state => state.Status == DocumentStatus.Done
                            && string.Equals(
                                NormalizeExtension(Path.GetExtension(state.DocumentPath)),
                                extension,
                                StringComparison.OrdinalIgnoreCase))
            .GroupBy(
                static state => $"{state.KnowledgeBaseId}::{state.DocumentPath}",
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderByDescending(static state => state.UpdatedAt).First())
            .Where(state => knowledgeBases.ContainsKey(state.KnowledgeBaseId))
            .Select(state => new AffectedDocument(knowledgeBases[state.KnowledgeBaseId], state))
            .ToList();
    }

    private async Task<IDocumentChunker> ResolveChunkerForViewAsync(DocumentIndexState state, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(state.ChunkerId)
            && chunkerRegistry.TryGetChunker(state.ChunkerId, out var fixedChunker)
            && fixedChunker is not null)
        {
            return fixedChunker;
        }

        return await chunkerRegistry.ResolveChunkerAsync(state.DocumentPath, ct);
    }

    private static DocumentChunkView BuildChunkView(
        string originalText,
        IReadOnlyList<DocumentChunk> chunks,
        string chunkerId,
        bool isPreview,
        string documentPath,
        string documentName,
        string? sourceKind,
        string? sourceGroupKey)
        => new()
        {
            DocumentPath = documentPath,
            DocumentName = documentName,
            OriginalText = originalText,
            Chunks = BuildHighlights(originalText, chunks),
            ChunkerId = chunkerId,
            IsPreview = isPreview,
            SourceKind = RAGIndexStateCoordinator.NormalizeSourceKind(sourceKind),
            SourceGroupKey = sourceGroupKey
        };

    private static IReadOnlyList<ChunkHighlight> BuildHighlights(
        string originalText,
        IReadOnlyList<DocumentChunk> chunks)
        => chunks
            .OrderBy(static chunk => chunk.ChunkIndex)
            .Select(chunk =>
            {
                var start = Math.Clamp(chunk.StartOffset, 0, originalText.Length);
                return new ChunkHighlight
                {
                    Index = chunk.ChunkIndex,
                    Start = start,
                    End = Math.Clamp(chunk.EndOffset, start, originalText.Length),
                    Section = chunk.SectionPath,
                    IsMatched = false
                };
            })
            .ToList();

    private static string NormalizeExtension(string extension)
    {
        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.Length == 0 || normalized.StartsWith(".", StringComparison.Ordinal)
            ? normalized
            : $".{normalized}";
    }

    private sealed record AffectedDocument(KnowledgeBaseModel KnowledgeBase, DocumentIndexState State);
}
