using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.Modules;
using AgentTextSearchResult = Microsoft.Agents.AI.TextSearchProvider.TextSearchResult;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Core RAG service - orchestrates document indexing and vector retrieval.
/// Infrastructure service: uses exceptions, not Res&lt;T&gt;.
/// </summary>
public sealed partial class RAGService(
    IDocumentIndexStateStore indexStateStore,
    IKnowledgeDocumentSourceStore documentSourceStore,
    RAGEmbeddingBindingResolver embeddingBindingResolver,
    RAGVectorCollectionCoordinator vectorCollectionCoordinator,
    RAGIndexStateCoordinator indexStateCoordinator,
    ChunkerRegistry chunkerRegistry,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGService> logger)
{
    private const int EmbeddingProgressBatchSize = 16;

    private readonly ModuleRAGOption _options = options.Value;
    private readonly ConcurrentDictionary<string, byte> _activeIndexingDocuments =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<KnowledgeBase> CreateKnowledgeBaseAsync(
        string name,
        string? description = null,
        string? searchToolDescription = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Knowledge base name cannot be empty.", nameof(name));
        }

        var kb = new KnowledgeBase
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            SearchToolDescription = searchToolDescription
        };

        await indexStateStore.UpsertKnowledgeBaseAsync(kb, ct);
        logger.LogInformation("Created knowledge base '{Name}' (Id: {Id})", kb.Name, kb.Id);
        return kb;
    }

    public async Task<KnowledgeBase> UpdateKnowledgeBaseAsync(
        string knowledgeBaseId,
        string name,
        string? description = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Knowledge base name cannot be empty.", nameof(name));
        }

        var kb = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);

        var updatedKb = kb with
        {
            Name = name.Trim(),
            Description = description
        };

        await indexStateStore.UpsertKnowledgeBaseAsync(updatedKb, ct);
        logger.LogInformation("Updated knowledge base '{KbId}'", knowledgeBaseId);
        return updatedKb;
    }

    /// <summary>
    /// Lists all knowledge bases from the store.
    /// </summary>
    public Task<IReadOnlyList<KnowledgeBase>> GetKnowledgeBasesAsync(CancellationToken ct = default)
        => indexStateStore.GetKnowledgeBasesAsync(ct);

    /// <summary>
    /// Gets a knowledge base by id.
    /// </summary>
    public Task<KnowledgeBase?> GetKnowledgeBaseByIdAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
        => indexStateStore.GetKnowledgeBaseAsync(knowledgeBaseId, ct);

    /// <summary>
    /// Persists the embedding provider/model for a knowledge base and optionally clears index data.
    /// </summary>
    public async Task SetKnowledgeBaseEmbeddingModelAsync(
        string knowledgeBaseId,
        string embeddingProviderId,
        string embeddingModelName,
        bool clearIndex = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(embeddingProviderId))
            throw new ArgumentException("Embedding provider ID cannot be empty.", nameof(embeddingProviderId));

        if (string.IsNullOrWhiteSpace(embeddingModelName))
            throw new ArgumentException("Embedding model name cannot be empty.", nameof(embeddingModelName));

        var kb = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);

        var normalizedProviderId = embeddingProviderId.Trim();
        var normalizedModelName = embeddingModelName.Trim();

        _ = await embeddingBindingResolver.ResolveAsync(
            kb with
            {
                EmbeddingProviderId = normalizedProviderId,
                EmbeddingModelName = normalizedModelName
            },
            ct);

        var changed = !string.Equals(kb.EmbeddingProviderId, normalizedProviderId, StringComparison.OrdinalIgnoreCase)
                      || !string.Equals(kb.EmbeddingModelName, normalizedModelName, StringComparison.OrdinalIgnoreCase);

        if (changed && clearIndex)
        {
            await vectorCollectionCoordinator.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
            await indexStateCoordinator.ResetKnowledgeBaseDocumentStatesForReindexAsync(knowledgeBaseId, ct);
            kb.DocumentCount = 0;
            kb.ChunkCount = 0;
        }

        kb.EmbeddingProviderId = normalizedProviderId;
        kb.EmbeddingModelName = normalizedModelName;
        await indexStateStore.UpsertKnowledgeBaseAsync(kb, ct);

        logger.LogInformation(
            "Persisted KB embedding binding for '{KbId}': provider '{ProviderId}', model '{ModelName}', clearIndex={ClearIndex}",
            knowledgeBaseId,
            normalizedProviderId,
            normalizedModelName,
            changed && clearIndex);
    }

    public async Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await vectorCollectionCoordinator.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
        await indexStateStore.DeleteKnowledgeBaseDocumentStatesAsync(knowledgeBaseId, ct);
        await indexStateStore.DeleteKnowledgeBaseAsync(knowledgeBaseId, ct);
        await documentSourceStore.DeleteKnowledgeBaseAsync(knowledgeBaseId, ct);

        logger.LogInformation("Deleted knowledge base {Id}", knowledgeBaseId);
    }

    public async Task<IReadOnlyList<DocumentQueueItem>> GetDocumentQueueAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        _ = await ConvergeInactiveIndexingDocumentsAsync(knowledgeBaseId, ct);
        return await indexStateCoordinator.GetDocumentQueueAsync(knowledgeBaseId, ct);
    }

    public async Task<int> ConvergeInactiveIndexingDocumentsAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        var recovered = await indexStateCoordinator.ConvergeInactiveIndexingDocumentsAsync(
            knowledgeBaseId,
            IsDocumentIndexingActiveAtRuntime,
            ct);

        if (recovered > 0)
        {
            logger.LogInformation(
                "Recovered {RecoveredCount} stale indexing document(s) in KB '{KbId}'",
                recovered,
                knowledgeBaseId);
        }

        return recovered;
    }

    public async Task<int> AddDocumentsToQueueAsync(
        string knowledgeBaseId,
        IEnumerable<string> documentPaths,
        string sourceKind = KnowledgeDocumentSourceKinds.Markdown,
        string? sourceGroupKey = null,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);

        return await indexStateCoordinator.AddDocumentsToQueueAsync(
            knowledgeBaseId,
            documentPaths,
            sourceKind,
            sourceGroupKey,
            ct);
    }

    public async Task<string?> GetDocumentSourceContentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        return await documentSourceStore.GetContentAsync(knowledgeBaseId, documentPath, ct);
    }

    public async Task QueueDocumentForReindexAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        var kb = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStateCoordinator.GetDocumentStateRequiredAsync(knowledgeBaseId, documentPath, ct);

        _ = await vectorCollectionCoordinator.RemoveIndexedDocumentDataAsync(kb, state, ct);
        await indexStateCoordinator.SetPendingStateAsync(state, resetChunkMetadata: true, ct);
        await indexStateCoordinator.RefreshKnowledgeBaseStatsAsync(kb, ct);
    }

    public async Task MarkDocumentPendingAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStateCoordinator.GetDocumentStateRequiredAsync(knowledgeBaseId, documentPath, ct);

        await indexStateCoordinator.SetPendingStateAsync(state, resetChunkMetadata: false, ct);
    }

    public async Task MarkDocumentIndexingAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStateCoordinator.GetDocumentStateRequiredAsync(knowledgeBaseId, documentPath, ct);

        await indexStateCoordinator.SetIndexingStateAsync(
            state,
            state.DocumentName,
            RAGIndexStateCoordinator.NormalizeSourceKind(state.SourceKind),
            state.SourceGroupKey,
            ct);
    }

    public async Task UpdateDocumentIndexingProgressAsync(
        string knowledgeBaseId,
        string documentPath,
        int progress,
        int chunkCount,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var state = await indexStateCoordinator.GetDocumentStateRequiredAsync(knowledgeBaseId, documentPath, ct);

        await indexStateCoordinator.SetIndexingProgressAsync(state, progress, chunkCount, ct);
    }

    public async Task MarkDocumentFailedAsync(
        string knowledgeBaseId,
        string documentPath,
        string errorMessage,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        await indexStateCoordinator.MarkDocumentFailedAsync(knowledgeBaseId, documentPath, errorMessage, ct);
    }

    public async Task RemoveDocumentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        var kb = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var existingState = await indexStateCoordinator.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct);

        _ = await vectorCollectionCoordinator.RemoveIndexedDocumentDataAsync(kb, existingState, ct);

        await indexStateCoordinator.DeleteDocumentStateAsync(knowledgeBaseId, documentPath, ct);
        await documentSourceStore.DeleteContentAsync(knowledgeBaseId, documentPath, ct);
        await indexStateCoordinator.RefreshKnowledgeBaseStatsAsync(kb, ct);
    }

    public async Task IndexDocumentAsync(
        string knowledgeBaseId,
        string documentPath,
        string documentTitle,
        string content,
        Func<IndexingProgress, CancellationToken, Task>? progressCallback = null,
        CancellationToken ct = default,
        string? sourceKind = null,
        string? sourceGroupKey = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Cannot index an empty document.");
        }

        var kb = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var existingState = await indexStateCoordinator.GetDocumentStateAsync(knowledgeBaseId, documentPath, ct);

        var resolvedDocumentName = RAGIndexStateCoordinator.ResolveDocumentName(documentTitle, documentPath);
        var resolvedSourceKind = RAGIndexStateCoordinator.NormalizeSourceKind(sourceKind ?? existingState?.SourceKind);
        var resolvedSourceGroupKey = sourceGroupKey ?? existingState?.SourceGroupKey;

        var activeState = existingState ?? new DocumentIndexState
        {
            KnowledgeBaseId = knowledgeBaseId,
            DocumentPath = documentPath,
            DocumentName = resolvedDocumentName
        };

        var activeDocumentKey = BuildActiveIndexingDocumentKey(knowledgeBaseId, documentPath);
        MarkDocumentIndexingStarted(activeDocumentKey);

        try
        {
            await indexStateCoordinator.SetIndexingStateAsync(
                activeState,
                resolvedDocumentName,
                resolvedSourceKind,
                resolvedSourceGroupKey,
                ct);

            var chunker = await chunkerRegistry.ResolveChunkerAsync(documentPath, ct);
            var chunks = chunker.ChunkDocument(content, documentPath, resolvedDocumentName);
            if (chunks.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Chunker '{chunker.ChunkerId}' produced no chunks for '{documentPath}'.");
            }

            var binding = await embeddingBindingResolver.ResolveAsync(kb, ct);
            var embeddingGenerator = embeddingBindingResolver.GetEmbeddingGenerator(binding);
            var collection = await vectorCollectionCoordinator.GetOrCreateCollectionAsync(kb, binding, ct);

            logger.LogInformation(
                "Generating embeddings for KB '{KbId}' using provider '{ProviderId}', model '{ModelName}' for {ChunkCount} chunks",
                kb.Id,
                binding.ProviderId,
                binding.ModelName,
                chunks.Count);

            var vectors = new List<float[]>(chunks.Count);
            var processedChunks = 0;
            await ReportProgressAsync(
                progressCallback,
                new IndexingProgress(processedChunks, chunks.Count, resolvedDocumentName),
                ct);

            foreach (var chunkBatch in chunks.Chunk(EmbeddingProgressBatchSize))
            {
                ct.ThrowIfCancellationRequested();

                var batchTexts = chunkBatch.Select(chunk => chunk.Content).ToList();
                var generatedBatch = await embeddingGenerator.GenerateAsync(batchTexts, cancellationToken: ct);

                var batchVectors = generatedBatch
                    .Select(embedding => embedding.Vector.ToArray())
                    .ToList();

                if (batchVectors.Count != batchTexts.Count)
                {
                    throw new InvalidOperationException(
                        $"Embedding generator returned {batchVectors.Count} vectors for {batchTexts.Count} chunks.");
                }

                vectors.AddRange(batchVectors);
                processedChunks += batchVectors.Count;
                await ReportProgressAsync(
                    progressCallback,
                    new IndexingProgress(processedChunks, chunks.Count, resolvedDocumentName),
                    ct);
            }

            _ = await vectorCollectionCoordinator.RemoveIndexedDocumentDataAsync(kb, existingState, ct);

            var records = chunks.Select((chunk, index) => new Dictionary<string, object?>
                {
                    ["Key"] = RAGVectorCollectionCoordinator.BuildRecordKey(knowledgeBaseId, documentPath, chunk.ChunkIndex),
                    ["KnowledgeBaseId"] = knowledgeBaseId,
                    ["DocumentPath"] = documentPath,
                    ["DocumentTitle"] = resolvedDocumentName,
                    ["Content"] = chunk.Content,
                    ["SectionPath"] = chunk.SectionPath,
                    ["ChunkIndex"] = chunk.ChunkIndex,
                    ["ChunkStart"] = chunk.StartOffset,
                    ["ChunkEnd"] = chunk.EndOffset,
                    ["ChunkerId"] = chunker.ChunkerId,
                    ["ContentEmbedding"] = vectors[index]
                })
                .ToList();

            await collection.UpsertAsync(records, ct);
            await documentSourceStore.SaveContentAsync(knowledgeBaseId, documentPath, content, ct);

            await indexStateCoordinator.SetDoneStateAsync(activeState, chunks.Count, chunker.ChunkerId, ct);
            await indexStateCoordinator.RefreshKnowledgeBaseStatsAsync(kb, ct);

            logger.LogInformation(
                "Indexed document '{Title}' into KB '{KbId}': {ChunkCount} chunks by '{ChunkerId}'",
                resolvedDocumentName,
                knowledgeBaseId,
                chunks.Count,
                chunker.ChunkerId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await TryMarkDocumentFailedAsync(knowledgeBaseId, documentPath, ex.Message, ct);
            throw;
        }
        finally
        {
            MarkDocumentIndexingCompleted(activeDocumentKey);
        }
    }

    public async Task<IReadOnlyList<TextSearchResult>> SearchAsync(
        string query,
        IEnumerable<string> knowledgeBaseIds,
        int topK = 0,
        CancellationToken ct = default)
    {
        if (topK <= 0)
        {
            topK = _options.DefaultTopK;
        }

        var results = new List<TextSearchResult>();

        foreach (var kbId in knowledgeBaseIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var kb = await indexStateStore.GetKnowledgeBaseAsync(kbId, ct);
            if (kb is null)
            {
                continue;
            }

            RAGEmbeddingBinding binding;
            try
            {
                binding = await embeddingBindingResolver.ResolveAsync(kb, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping KB '{KbId}' in search because embedding binding is invalid.", kbId);
                continue;
            }

            var collection = await vectorCollectionCoordinator.GetOrCreateCollectionAsync(kb, binding, ct);
            if (!await collection.CollectionExistsAsync(ct))
            {
                continue;
            }

            var hybridSearch = collection.GetService(typeof(IKeywordHybridSearchable<Dictionary<string, object?>>))
                               as IKeywordHybridSearchable<Dictionary<string, object?>>;

            IAsyncEnumerable<VectorSearchResult<Dictionary<string, object?>>> searchResults;
            if (hybridSearch is not null)
            {
                var keywords = WordSegmenter().Matches(query).Select(m => m.Value).ToList();
                searchResults = hybridSearch.HybridSearchAsync(
                    query,
                    keywords,
                    top: topK,
                    cancellationToken: ct);
            }
            else
            {
                searchResults = collection.SearchAsync(query, top: topK, cancellationToken: ct);
            }

            await foreach (var result in searchResults)
            {
                var record = result.Record;
                results.Add(new TextSearchResult
                {
                    SourceName = record["DocumentTitle"]?.ToString(),
                    SourceLink = record["DocumentPath"]?.ToString(),
                    Text = record["Content"]?.ToString() ?? string.Empty,
                    Score = result.Score,
                    KnowledgeBaseId = kbId,
                    SectionPath = record["SectionPath"]?.ToString(),
                    DocumentId = record["DocumentPath"]?.ToString(),
                    ChunkIndex = TryGetChunkIndex(record.TryGetValue("ChunkIndex", out var value)
                        ? value
                        : null)
                });
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Creates a search adapter delegate compatible with TextSearchProvider constructor.
    /// Maps our TextSearchResult to the agent-framework's TextSearchResult.
    /// </summary>
    public Func<string, CancellationToken, Task<IEnumerable<AgentTextSearchResult>>>
        CreateSearchAdapter(IEnumerable<string> knowledgeBaseIds, int topK = 0)
    {
        var kbIds = knowledgeBaseIds.ToList();
        return async (query, ct) =>
        {
            var results = await SearchAsync(query, kbIds, topK, ct);
            return results.Select(r => new AgentTextSearchResult
            {
                SourceName = r.SectionPath is not null
                    ? $"{r.SourceName} > {r.SectionPath}"
                    : r.SourceName,
                SourceLink = r.SourceLink,
                Text = r.Text,
                RawRepresentation = r
            });
        };
    }

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

        var content = await documentSourceStore.GetContentAsync(knowledgeBaseId, documentPath, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var chunker = await ResolveChunkerForViewAsync(state, ct);
        var chunks = chunker.ChunkDocument(content, state.DocumentPath, state.DocumentName);
        return BuildChunkView(content, chunks, chunker.ChunkerId, isPreview: false);
    }

    public async Task<DocumentChunkView> BuildDocumentChunkPreviewAsync(
        string knowledgeBaseId,
        string documentPath,
        string documentTitle,
        string content,
        CancellationToken ct = default)
    {
        _ = await indexStateCoordinator.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Cannot build chunk preview from empty content.");
        }

        var chunker = await chunkerRegistry.ResolveChunkerAsync(documentPath, ct);
        var chunks = chunker.ChunkDocument(content, documentPath, documentTitle);
        return BuildChunkView(content, chunks, chunker.ChunkerId, isPreview: true);
    }

    public Task<ChunkerManagementState> GetChunkerManagementStateAsync(CancellationToken ct = default)
        => chunkerRegistry.GetManagementStateAsync(ct);

    public async Task<ChunkerRoutingChangePreview> PreviewChunkerRoutingChangeAsync(
        string extension,
        string targetChunkerId,
        CancellationToken ct = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var routes = await chunkerRegistry.GetRoutesAsync(ct);
        var route = routes.FirstOrDefault(x =>
            string.Equals(x.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException($"No chunker route available for extension '{normalizedExtension}'.");

        if (!route.CandidateChunkerIds.Contains(targetChunkerId, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Chunker '{targetChunkerId}' does not support extension '{normalizedExtension}'.");
        }

        var affected = await GetAffectedDocumentsByExtensionAsync(normalizedExtension, ct);
        return new ChunkerRoutingChangePreview
        {
            Extension = normalizedExtension,
            CurrentChunkerId = route.DefaultChunkerId,
            TargetChunkerId = targetChunkerId,
            AffectedDocumentCount = affected.Count,
            AffectedKnowledgeBaseIds = affected
                .Select(x => x.KnowledgeBase.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public async Task<ChunkerRoutingApplyResult> ApplyChunkerRoutingChangeAsync(
        string extension,
        string targetChunkerId,
        CancellationToken ct = default)
    {
        var preview = await PreviewChunkerRoutingChangeAsync(extension, targetChunkerId, ct);
        if (string.Equals(preview.CurrentChunkerId, preview.TargetChunkerId, StringComparison.OrdinalIgnoreCase))
        {
            return new ChunkerRoutingApplyResult
            {
                Extension = preview.Extension,
                TargetChunkerId = preview.TargetChunkerId,
                MarkedPendingDocumentCount = 0
            };
        }

        var affectedDocuments = await GetAffectedDocumentsByExtensionAsync(preview.Extension, ct);
        var markedPendingCount = 0;
        var affectedKnowledgeBaseIds = affectedDocuments
            .Select(x => x.KnowledgeBase.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var affected in affectedDocuments)
        {
            var kb = affected.KnowledgeBase;
            var state = affected.State;

            await vectorCollectionCoordinator.RemoveIndexedDocumentDataAsync(kb, state, ct);
            await indexStateCoordinator.SetPendingStateAsync(state, resetChunkMetadata: true, ct);
            markedPendingCount++;
        }

        foreach (var kbId in affectedKnowledgeBaseIds)
        {
            var kb = await indexStateStore.GetKnowledgeBaseAsync(kbId, ct);
            if (kb is null)
            {
                continue;
            }

            await indexStateCoordinator.RefreshKnowledgeBaseStatsAsync(kb, ct);
        }

        await chunkerRegistry.SetDefaultChunkerAsync(preview.Extension, preview.TargetChunkerId, ct);

        return new ChunkerRoutingApplyResult
        {
            Extension = preview.Extension,
            TargetChunkerId = preview.TargetChunkerId,
            MarkedPendingDocumentCount = markedPendingCount
        };
    }

    public async Task<ChunkerTestResult> TestChunkerAsync(
        string chunkerId,
        string documentName,
        string content,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chunkerId))
        {
            throw new ArgumentException("Chunker ID cannot be empty.", nameof(chunkerId));
        }

        if (!chunkerRegistry.TryGetChunker(chunkerId, out var chunker) || chunker is null)
        {
            throw new KeyNotFoundException($"Chunker '{chunkerId}' not found.");
        }

        var resolvedDocumentName = string.IsNullOrWhiteSpace(documentName)
            ? "sample.md"
            : documentName.Trim();
        var title = Path.GetFileNameWithoutExtension(resolvedDocumentName);
        var originalText = content ?? string.Empty;
        var chunks = chunker.ChunkDocument(originalText, resolvedDocumentName, title);

        return new ChunkerTestResult
        {
            ChunkerId = chunker.ChunkerId,
            DocumentName = resolvedDocumentName,
            OriginalText = originalText,
            Chunks = BuildChunkHighlights(originalText, chunks)
        };
    }

    #region Private helpers

    private async Task<List<AffectedDocument>> GetAffectedDocumentsByExtensionAsync(
        string extension,
        CancellationToken ct)
    {
        var normalized = NormalizeExtension(extension);
        var states = await indexStateStore.GetAllDocumentStatesAsync(ct);
        var knowledgeBases = await indexStateStore.GetKnowledgeBasesAsync(ct);
        var knowledgeBaseLookup = knowledgeBases.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        var affectedStates = states
            .Where(state =>
                state.Status == DocumentStatus.Done &&
                string.Equals(
                    NormalizeExtension(Path.GetExtension(state.DocumentPath)),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            .GroupBy(
                state => $"{state.KnowledgeBaseId}::{state.DocumentPath}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(state => state.UpdatedAt)
                .First())
            .ToList();

        var affected = new List<AffectedDocument>();
        foreach (var state in affectedStates)
        {
            if (!knowledgeBaseLookup.TryGetValue(state.KnowledgeBaseId, out var kb))
            {
                continue;
            }

            affected.Add(new AffectedDocument(kb, state));
        }

        return affected;
    }

    private async Task TryMarkDocumentFailedAsync(
        string knowledgeBaseId,
        string documentPath,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            await indexStateCoordinator.MarkDocumentFailedAsync(knowledgeBaseId, documentPath, errorMessage, ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to persist indexing error state for document '{DocumentPath}' in KB '{KbId}'",
                documentPath,
                knowledgeBaseId);
        }
    }

    private static string BuildActiveIndexingDocumentKey(string knowledgeBaseId, string documentPath)
        => $"{knowledgeBaseId}::{documentPath}";

    private bool IsDocumentIndexingActiveAtRuntime(string knowledgeBaseId, string documentPath)
        => _activeIndexingDocuments.ContainsKey(BuildActiveIndexingDocumentKey(knowledgeBaseId, documentPath));

    private void MarkDocumentIndexingStarted(string activeDocumentKey)
        => _activeIndexingDocuments[activeDocumentKey] = 0;

    private void MarkDocumentIndexingCompleted(string activeDocumentKey)
        => _activeIndexingDocuments.TryRemove(activeDocumentKey, out _);

    private static Task ReportProgressAsync(
        Func<IndexingProgress, CancellationToken, Task>? progressCallback,
        IndexingProgress progress,
        CancellationToken ct)
    {
        if (progressCallback is null)
        {
            return Task.CompletedTask;
        }

        return progressCallback(progress, ct);
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
        bool isPreview)
    {
        return new DocumentChunkView
        {
            OriginalText = originalText,
            Chunks = BuildChunkHighlights(originalText, chunks),
            ChunkerId = chunkerId,
            IsPreview = isPreview
        };
    }

    private static IReadOnlyList<ChunkHighlight> BuildChunkHighlights(
        string originalText,
        IReadOnlyList<DocumentChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return [];
        }

        var maxLength = originalText.Length;
        return chunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk =>
            {
                var start = Math.Clamp(chunk.StartOffset, 0, maxLength);
                var end = Math.Clamp(chunk.EndOffset, start, maxLength);
                return new ChunkHighlight
                {
                    Index = chunk.ChunkIndex,
                    Start = start,
                    End = end,
                    Section = chunk.SectionPath,
                    IsMatched = false
                };
            })
            .ToList();
    }

    private static int? TryGetChunkIndex(object? rawChunkIndex)
    {
        if (rawChunkIndex is null)
        {
            return null;
        }

        return rawChunkIndex switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
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

    [GeneratedRegex(@"\p{L}+", RegexOptions.IgnoreCase)]
    private static partial Regex WordSegmenter();

    private readonly record struct AffectedDocument(
        KnowledgeBase KnowledgeBase,
        DocumentIndexState State);

    #endregion
}
