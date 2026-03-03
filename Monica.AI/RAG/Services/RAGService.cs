using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.AI.Modules;
using AgentTextSearchResult = Microsoft.Agents.AI.TextSearchProvider.TextSearchResult;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Core RAG service - orchestrates document indexing and vector retrieval.
/// Infrastructure service: uses exceptions, not Res&lt;T&gt;.
/// </summary>
public sealed partial class RAGService(
    IKnowledgeBaseStore kbStore,
    IDocumentQueueStore documentQueueStore,
    IDocumentChunkSnapshotStore chunkSnapshotStore,
    VectorStore vectorStore,
    IAIProviderFactory providerFactory,
    ChunkerRegistry chunkerRegistry,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGService> logger)
{
    private const int LegacyDeleteProbeLimit = 20_000;
    private const int LegacyDeleteMissThreshold = 32;

    private readonly ModuleRAGOption _options = options.Value;

    private readonly ConcurrentDictionary<string, VectorStoreCollection<object, Dictionary<string, object?>>> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _collectionBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _initializedCollections = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task<KnowledgeBase> CreateKnowledgeBaseAsync(
        string name, string? description = null,
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

        await kbStore.SaveAsync(kb, ct);
        logger.LogInformation("Created knowledge base '{Name}' (Id: {Id})", kb.Name, kb.Id);
        return kb;
    }

    /// <summary>
    /// Lists all knowledge bases from the store.
    /// </summary>
    public Task<IReadOnlyList<KnowledgeBase>> GetKnowledgeBasesAsync(CancellationToken ct = default)
        => kbStore.GetAllAsync(ct);

    /// <summary>
    /// Gets a knowledge base by id.
    /// </summary>
    public Task<KnowledgeBase?> GetKnowledgeBaseByIdAsync(string knowledgeBaseId, CancellationToken ct = default)
        => kbStore.GetByIdAsync(knowledgeBaseId, ct);

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

        var kb = await kbStore.GetByIdAsync(knowledgeBaseId, ct)
                 ?? throw new KeyNotFoundException(
                     $"Knowledge base '{knowledgeBaseId}' not found.");

        var normalizedProviderId = embeddingProviderId.Trim();
        var normalizedModelName = embeddingModelName.Trim();

        // Validate the target binding before mutating KB state.
        _ = ResolveEmbeddingBinding(kb with
        {
            EmbeddingProviderId = normalizedProviderId,
            EmbeddingModelName = normalizedModelName
        });

        var changed = !string.Equals(kb.EmbeddingProviderId, normalizedProviderId, StringComparison.OrdinalIgnoreCase)
                      || !string.Equals(kb.EmbeddingModelName, normalizedModelName, StringComparison.OrdinalIgnoreCase);

        if (changed && clearIndex)
        {
            await ClearCollectionCacheAndStorageAsync(GetCollectionName(knowledgeBaseId), ct);
            await chunkSnapshotStore.RemoveKnowledgeBaseAsync(knowledgeBaseId, ct);
            await ResetDocumentQueueForReindexAsync(knowledgeBaseId, ct);
            kb.DocumentCount = 0;
            kb.ChunkCount = 0;
        }

        kb.EmbeddingProviderId = normalizedProviderId;
        kb.EmbeddingModelName = normalizedModelName;
        await kbStore.SaveAsync(kb, ct);

        logger.LogInformation(
            "Persisted KB embedding binding for '{KbId}': provider '{ProviderId}', model '{ModelName}', clearIndex={ClearIndex}",
            knowledgeBaseId, normalizedProviderId, normalizedModelName, changed && clearIndex);
    }

    public async Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        await ClearCollectionCacheAndStorageAsync(GetCollectionName(knowledgeBaseId), ct);
        await chunkSnapshotStore.RemoveKnowledgeBaseAsync(knowledgeBaseId, ct);

        var queue = await documentQueueStore.GetQueueAsync(knowledgeBaseId, ct);
        foreach (var item in queue)
        {
            await documentQueueStore.RemoveAsync(knowledgeBaseId, item.Id, ct);
        }

        await kbStore.DeleteAsync(knowledgeBaseId, ct);
        logger.LogInformation("Deleted knowledge base {Id}", knowledgeBaseId);
    }

    public async Task RemoveDocumentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        var kb = await kbStore.GetByIdAsync(knowledgeBaseId, ct)
                 ?? throw new KeyNotFoundException($"Knowledge base '{knowledgeBaseId}' not found.");

        var existingSnapshot = await chunkSnapshotStore.GetAsync(knowledgeBaseId, documentPath, ct);
        var removedChunkCount = await RemoveIndexedDocumentDataAsync(kb, documentPath, existingSnapshot, ct);

        await documentQueueStore.RemoveAsync(knowledgeBaseId, documentPath, ct);

        if (existingSnapshot is not null || removedChunkCount > 0)
        {
            kb.DocumentCount = Math.Max(0, kb.DocumentCount - 1);
            kb.ChunkCount = Math.Max(0, kb.ChunkCount - removedChunkCount);
            await kbStore.SaveAsync(kb, ct);
        }
    }

    public async Task IndexDocumentAsync(
        string knowledgeBaseId,
        string documentPath,
        string documentTitle,
        string content,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken ct = default)
    {
        var kb = await kbStore.GetByIdAsync(knowledgeBaseId, ct)
                 ?? throw new KeyNotFoundException(
                     $"Knowledge base '{knowledgeBaseId}' not found.");

        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogWarning(
                "Skip indexing empty document '{DocumentPath}' for KB '{KbId}'",
                documentPath,
                kb.Id);
            return;
        }

        var chunker = await chunkerRegistry.ResolveChunkerAsync(documentPath, ct);

        var chunks = chunker.ChunkDocument(content, documentPath, documentTitle);
        if (chunks.Count == 0)
        {
            logger.LogWarning(
                "Chunker '{ChunkerId}' produced no chunks for '{DocumentPath}'",
                chunker.ChunkerId,
                documentPath);
            return;
        }

        var binding = ResolveEmbeddingBinding(kb);
        var embeddingGenerator = ResolveEmbeddingGenerator(binding);
        var collection = await GetOrCreateCollectionForKnowledgeBaseAsync(kb, binding, ct);

        logger.LogInformation(
            "Generating embeddings for KB '{KbId}' using provider '{ProviderId}', model '{ModelName}' for {ChunkCount} chunks",
            kb.Id, binding.ProviderId, binding.ModelName, chunks.Count);

        var generatedEmbeddings = await embeddingGenerator.GenerateAsync(
            chunks.Select(chunk => chunk.Content),
            cancellationToken: ct);

        var vectors = generatedEmbeddings
            .Select(embedding => embedding.Vector.ToArray())
            .ToList();

        if (vectors.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding generator returned {vectors.Count} vectors for {chunks.Count} chunks.");
        }

        var existingSnapshot = await chunkSnapshotStore.GetAsync(kb.Id, documentPath, ct);
        var removedChunkCount = await RemoveIndexedDocumentDataAsync(kb, documentPath, existingSnapshot, ct);
        var wasIndexedBefore = existingSnapshot is not null || removedChunkCount > 0;

        if (removedChunkCount > 0)
        {
            kb.ChunkCount = Math.Max(0, kb.ChunkCount - removedChunkCount);
        }

        // Build all records with pre-generated vectors.
        var records = chunks.Select((chunk, index) => new Dictionary<string, object?>
            {
                ["Key"] = BuildRecordKey(knowledgeBaseId, documentPath, chunk.ChunkIndex),
                ["KnowledgeBaseId"] = knowledgeBaseId,
                ["DocumentPath"] = documentPath,
                ["DocumentTitle"] = documentTitle,
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

        await chunkSnapshotStore.SaveAsync(
            new DocumentChunkSnapshot
            {
                KnowledgeBaseId = knowledgeBaseId,
                DocumentPath = documentPath,
                DocumentTitle = documentTitle,
                ChunkerId = chunker.ChunkerId,
                OriginalText = content,
                UpdatedAt = DateTimeOffset.UtcNow,
                Chunks = chunks
                    .Select(chunk => new DocumentChunkSnapshotItem
                    {
                        Index = chunk.ChunkIndex,
                        Start = chunk.StartOffset,
                        End = chunk.EndOffset,
                        Section = chunk.SectionPath
                    })
                    .ToList()
            },
            ct);

        if (!wasIndexedBefore)
        {
            kb.DocumentCount++;
        }

        kb.ChunkCount += chunks.Count;
        await kbStore.SaveAsync(kb, ct);

        progress?.Report(new IndexingProgress(chunks.Count, chunks.Count, documentTitle));

        logger.LogInformation(
            "Indexed document '{Title}' into KB '{KbId}': {ChunkCount} chunks by '{ChunkerId}'",
            documentTitle, knowledgeBaseId, chunks.Count, chunker.ChunkerId);
    }

    public async Task<IReadOnlyList<TextSearchResult>> SearchAsync(
        string query,
        IEnumerable<string> knowledgeBaseIds,
        int topK = 0,
        CancellationToken ct = default)
    {
        if (topK <= 0) topK = _options.DefaultTopK;
        var results = new List<TextSearchResult>();

        foreach (var kbId in knowledgeBaseIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var kb = await kbStore.GetByIdAsync(kbId, ct);
            if (kb is null)
            {
                continue;
            }

            EmbeddingBinding binding;
            try
            {
                binding = ResolveEmbeddingBinding(kb);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Skipping KB '{KbId}' in search because embedding binding is invalid.",
                    kbId);
                continue;
            }

            var collection = await GetOrCreateCollectionForKnowledgeBaseAsync(kb, binding, ct);

            if (!await collection.CollectionExistsAsync(ct))
            {
                continue;
            }

            var hybridSearch = collection.GetService(
                typeof(IKeywordHybridSearchable<Dictionary<string, object?>>))
                as IKeywordHybridSearchable<Dictionary<string, object?>>;

            IAsyncEnumerable<VectorSearchResult<Dictionary<string, object?>>> searchResults;

            if (hybridSearch is not null)
            {
                var keywords = WordSegmenter().Matches(query).Select(m => m.Value).ToList();
                searchResults = hybridSearch.HybridSearchAsync(
                    query, keywords, top: topK, cancellationToken: ct);
            }
            else
            {
                searchResults = collection.SearchAsync(
                    query, top: topK, cancellationToken: ct);
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

    /// <summary>
    /// Creates an AIFunction for use as a chat tool (OnDemandFunctionCalling mode).
    /// </summary>
    public AIFunction CreateSearchTool(KnowledgeBase knowledgeBase)
    {
        var toolDescription = knowledgeBase.SearchToolDescription
            ?? $"Search the '{knowledgeBase.Name}' knowledge base for relevant information.";

        return AIFunctionFactory.Create(
            async (string query, CancellationToken ct) =>
            {
                var results = await SearchAsync(query, [knowledgeBase.Id], ct: ct);
                return FormatSearchResults(results);
            },
            $"Search_{knowledgeBase.Id}",
            toolDescription);
    }

    public async Task<DocumentChunkView?> GetDocumentChunkViewAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        _ = await kbStore.GetByIdAsync(knowledgeBaseId, ct)
            ?? throw new KeyNotFoundException($"Knowledge base '{knowledgeBaseId}' not found.");

        var snapshot = await chunkSnapshotStore.GetAsync(knowledgeBaseId, documentPath, ct);
        if (snapshot is null)
        {
            return null;
        }

        return BuildChunkViewFromSnapshot(snapshot, isPreview: false);
    }

    public async Task<DocumentChunkView> BuildDocumentChunkPreviewAsync(
        string knowledgeBaseId,
        string documentPath,
        string documentTitle,
        string content,
        CancellationToken ct = default)
    {
        _ = await kbStore.GetByIdAsync(knowledgeBaseId, ct)
            ?? throw new KeyNotFoundException($"Knowledge base '{knowledgeBaseId}' not found.");

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
            var snapshot = affected.Snapshot;
            var queueItem = affected.QueueItem ?? BuildQueueItemFromSnapshot(snapshot);

            await RemoveIndexedDocumentDataAsync(kb, snapshot.DocumentPath, snapshot, ct);

            MarkQueueItemPending(queueItem, snapshot.OriginalText);

            if (affected.QueueItem is null)
            {
                await documentQueueStore.AddAsync(queueItem, ct);
            }
            else
            {
                await documentQueueStore.UpdateAsync(queueItem, ct);
            }

            markedPendingCount++;
        }

        foreach (var kbId in affectedKnowledgeBaseIds)
        {
            var kb = await kbStore.GetByIdAsync(kbId, ct);
            if (kb is null)
            {
                continue;
            }

            var indexedSnapshots = await chunkSnapshotStore.GetByKnowledgeBaseAsync(kbId, ct);
            kb.DocumentCount = indexedSnapshots.Count;
            kb.ChunkCount = indexedSnapshots.Sum(snapshot => snapshot.Chunks.Count);
            await kbStore.SaveAsync(kb, ct);
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

    private static string BuildBindingKey(string providerId, string modelName, int dimensions)
        => $"{providerId}::{modelName}::{dimensions}";

    private static string BuildRecordKey(string knowledgeBaseId, string documentPath, int chunkIndex)
        => $"{knowledgeBaseId}_{documentPath}_{chunkIndex}";

    private string GetCollectionName(string knowledgeBaseId)
        => $"{_options.CollectionNamePrefix}{knowledgeBaseId}";

    private Task<VectorStoreCollection<object, Dictionary<string, object?>>> GetOrCreateCollectionForKnowledgeBaseAsync(
        KnowledgeBase kb,
        CancellationToken ct)
    {
        var binding = ResolveEmbeddingBinding(kb);
        return GetOrCreateCollectionForKnowledgeBaseAsync(kb, binding, ct);
    }

    private async Task<VectorStoreCollection<object, Dictionary<string, object?>>> GetOrCreateCollectionForKnowledgeBaseAsync(
        KnowledgeBase kb,
        EmbeddingBinding binding,
        CancellationToken ct)
    {
        var collectionName = GetCollectionName(kb.Id);
        var bindingKey = BuildBindingKey(binding.ProviderId, binding.ModelName, binding.Dimensions);

        if (_collectionBindings.TryGetValue(collectionName, out var existingBindingKey)
            && !string.Equals(existingBindingKey, bindingKey, StringComparison.OrdinalIgnoreCase))
        {
            await ClearCollectionCacheAndStorageAsync(collectionName, ct);
        }

        var collection = _collections.GetOrAdd(collectionName, _ =>
        {
            var embeddingGenerator = ResolveEmbeddingGenerator(binding);
            var definition = CreateCollectionDefinition(binding.Dimensions, embeddingGenerator);
            return vectorStore.GetDynamicCollection(collectionName, definition);
        });

        _collectionBindings[collectionName] = bindingKey;
        await EnsureCollectionInitializedAsync(collectionName, collection, ct);
        return collection;
    }

    private EmbeddingBinding ResolveEmbeddingBinding(KnowledgeBase kb)
    {
        if (string.IsNullOrWhiteSpace(kb.EmbeddingProviderId) || string.IsNullOrWhiteSpace(kb.EmbeddingModelName))
        {
            throw new InvalidOperationException(
                $"Knowledge base '{kb.Name}' has no embedding model configured. Configure provider and model before indexing or searching.");
        }

        var provider = providerFactory.GetProvider(kb.EmbeddingProviderId)
                       ?? throw new InvalidOperationException(
                           $"Embedding provider '{kb.EmbeddingProviderId}' configured on knowledge base '{kb.Name}' was not found.");

        var embeddingModel = provider.Info.SupportedModels?
            .OfType<EmbeddingModelInfo>()
            .FirstOrDefault(model =>
                string.Equals(model.ModelName, kb.EmbeddingModelName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Embedding model '{kb.EmbeddingModelName}' is not configured on provider '{provider.ProviderId}'.");

        var dimensions = embeddingModel.Dimensions ?? throw new InvalidOperationException(
                $"Embedding model '{embeddingModel.ModelName}' has no dimensions configured. " +
                "Use provider model probe.");

        return new EmbeddingBinding(provider.ProviderId, embeddingModel.ModelName, dimensions);
    }

    private IEmbeddingGenerator<string, Embedding<float>> ResolveEmbeddingGenerator(
        EmbeddingBinding binding)
    {
        var provider = providerFactory.GetProvider(binding.ProviderId)
                       ?? throw new InvalidOperationException(
                           $"Embedding provider '{binding.ProviderId}' not found.");

        return provider.GetEmbeddingGenerator(binding.ModelName);
    }

    private async Task<int> RemoveIndexedDocumentDataAsync(
        KnowledgeBase kb,
        string documentPath,
        DocumentChunkSnapshot? existingSnapshot,
        CancellationToken ct)
    {
        VectorStoreCollection<object, Dictionary<string, object?>>? collection = null;
        try
        {
            collection = await GetOrCreateCollectionForKnowledgeBaseAsync(kb, ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Skip vector cleanup for document '{DocumentPath}' in KB '{KbId}' because collection cannot be resolved.",
                documentPath,
                kb.Id);
        }

        if (existingSnapshot is not null)
        {
            if (collection is not null && await collection.CollectionExistsAsync(ct) && existingSnapshot.Chunks.Count > 0)
            {
                var keys = existingSnapshot.Chunks
                    .Select(chunk => (object)BuildRecordKey(kb.Id, documentPath, chunk.Index))
                    .ToList();
                await collection.DeleteAsync(keys, ct);
            }

            await chunkSnapshotStore.RemoveAsync(kb.Id, documentPath, ct);
            return existingSnapshot.Chunks.Count;
        }

        if (collection is null || !await collection.CollectionExistsAsync(ct))
        {
            return 0;
        }

        return await RemoveRecordsByKeyProbeAsync(collection, kb.Id, documentPath, ct);
    }

    private static async Task<int> RemoveRecordsByKeyProbeAsync(
        VectorStoreCollection<object, Dictionary<string, object?>> collection,
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct)
    {
        var removed = 0;
        var misses = 0;

        for (var i = 0; i < LegacyDeleteProbeLimit; i++)
        {
            var key = (object)BuildRecordKey(knowledgeBaseId, documentPath, i);
            var existing = await collection.GetAsync(key, cancellationToken: ct);

            if (existing is null)
            {
                misses++;
                if (i > LegacyDeleteMissThreshold && misses >= LegacyDeleteMissThreshold)
                {
                    break;
                }

                continue;
            }

            misses = 0;
            await collection.DeleteAsync(key, ct);
            removed++;
        }

        return removed;
    }

    private async Task ClearCollectionCacheAndStorageAsync(string collectionName, CancellationToken ct)
    {
        await vectorStore.EnsureCollectionDeletedAsync(collectionName, ct);
        _collections.TryRemove(collectionName, out _);
        _collectionBindings.TryRemove(collectionName, out _);
        _initializedCollections.TryRemove(collectionName, out _);
    }

    private async Task ResetDocumentQueueForReindexAsync(string kbId, CancellationToken ct)
    {
        var queueItems = await documentQueueStore.GetQueueAsync(kbId, ct);
        foreach (var item in queueItems)
        {
            item.Status = DocumentStatus.Pending;
            item.ChunkCount = 0;
            item.Progress = 0;
            item.IndexedAt = null;
            item.ErrorMessage = null;
            await documentQueueStore.UpdateAsync(item, ct);
        }
    }

    /// <summary>
    /// Thread-safe collection initialization - EnsureCollectionExistsAsync called at most once per collection.
    /// </summary>
    private async Task EnsureCollectionInitializedAsync(
        string collectionName,
        VectorStoreCollection<object, Dictionary<string, object?>> collection,
        CancellationToken ct)
    {
        if (_initializedCollections.ContainsKey(collectionName)) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initializedCollections.ContainsKey(collectionName)) return;
            await collection.EnsureCollectionExistsAsync(ct);
            _initializedCollections[collectionName] = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<List<AffectedDocument>> GetAffectedDocumentsByExtensionAsync(
        string extension,
        CancellationToken ct)
    {
        var normalized = NormalizeExtension(extension);
        var snapshots = await chunkSnapshotStore.GetAllAsync(ct);
        var knowledgeBases = await kbStore.GetAllAsync(ct);
        var knowledgeBaseLookup = knowledgeBases.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var affected = new List<AffectedDocument>();

        var latestSnapshots = snapshots
            .Where(snapshot =>
                string.Equals(
                    NormalizeExtension(Path.GetExtension(snapshot.DocumentPath)),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            .GroupBy(
                snapshot => $"{snapshot.KnowledgeBaseId}::{snapshot.DocumentPath}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(snapshot => snapshot.UpdatedAt)
                .First())
            .ToList();

        var snapshotsByKnowledgeBase = latestSnapshots
            .GroupBy(snapshot => snapshot.KnowledgeBaseId, StringComparer.OrdinalIgnoreCase);

        foreach (var snapshotGroup in snapshotsByKnowledgeBase)
        {
            if (!knowledgeBaseLookup.TryGetValue(snapshotGroup.Key, out var kb))
            {
                continue;
            }

            var queue = await documentQueueStore.GetQueueAsync(kb.Id, ct);
            var queueLookup = queue.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var snapshot in snapshotGroup)
            {
                queueLookup.TryGetValue(snapshot.DocumentPath, out var queueItem);
                affected.Add(new AffectedDocument(kb, snapshot, queueItem));
            }
        }

        return affected;
    }

    private static DocumentQueueItem BuildQueueItemFromSnapshot(DocumentChunkSnapshot snapshot)
    {
        var resolvedName = string.IsNullOrWhiteSpace(snapshot.DocumentTitle)
            ? Path.GetFileName(snapshot.DocumentPath)
            : snapshot.DocumentTitle.Trim();

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = snapshot.DocumentPath;
        }

        return new DocumentQueueItem
        {
            Id = snapshot.DocumentPath,
            Name = resolvedName,
            KnowledgeBaseId = snapshot.KnowledgeBaseId,
            OriginalText = snapshot.OriginalText
        };
    }

    private static void MarkQueueItemPending(DocumentQueueItem queueItem, string originalText)
    {
        queueItem.Status = DocumentStatus.Pending;
        queueItem.Progress = 0;
        queueItem.ChunkCount = 0;
        queueItem.IndexedAt = null;
        queueItem.ErrorMessage = null;
        queueItem.OriginalText ??= originalText;
    }

    private static string FormatSearchResults(IReadOnlyList<TextSearchResult> results)
    {
        if (results.Count == 0) return "No relevant results found.";

        return string.Join("\n\n---\n\n", results.Select((r, i) =>
            $"[{i + 1}] {r.SourceName}" +
            (r.SectionPath is not null ? $" > {r.SectionPath}" : "") +
            $"\n{r.Text}" +
            (r.SourceLink is not null ? $"\nSource: {r.SourceLink}" : "")));
    }

    private static VectorStoreCollectionDefinition CreateCollectionDefinition(
        int vectorDimensions,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        return new VectorStoreCollectionDefinition
        {
            EmbeddingGenerator = embeddingGenerator,
            Properties =
            [
                new VectorStoreKeyProperty("Key", typeof(string)),
                new VectorStoreDataProperty("KnowledgeBaseId", typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty("DocumentPath", typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty("DocumentTitle", typeof(string)),
                new VectorStoreDataProperty("Content", typeof(string)) { IsFullTextIndexed = true },
                new VectorStoreDataProperty("SectionPath", typeof(string)),
                new VectorStoreDataProperty("ChunkIndex", typeof(int)),
                new VectorStoreDataProperty("ChunkStart", typeof(int)),
                new VectorStoreDataProperty("ChunkEnd", typeof(int)),
                new VectorStoreDataProperty("ChunkerId", typeof(string)),
                new VectorStoreVectorProperty("ContentEmbedding", typeof(float[]), vectorDimensions)
                {
                    EmbeddingGenerator = embeddingGenerator
                }
            ]
        };
    }

    private static DocumentChunkView BuildChunkViewFromSnapshot(DocumentChunkSnapshot snapshot, bool isPreview)
    {
        var content = snapshot.OriginalText ?? string.Empty;
        var highlights = snapshot.Chunks
            .OrderBy(chunk => chunk.Index)
            .Select(chunk =>
            {
                var start = Math.Clamp(chunk.Start, 0, content.Length);
                var end = Math.Clamp(chunk.End, start, content.Length);
                return new ChunkHighlight
                {
                    Index = chunk.Index,
                    Start = start,
                    End = end,
                    Section = chunk.Section,
                    IsMatched = false
                };
            })
            .ToList();

        return new DocumentChunkView
        {
            OriginalText = content,
            Chunks = highlights,
            ChunkerId = snapshot.ChunkerId,
            IsPreview = isPreview
        };
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

    private readonly record struct EmbeddingBinding(string ProviderId, string ModelName, int Dimensions);
    private readonly record struct AffectedDocument(
        KnowledgeBase KnowledgeBase,
        DocumentChunkSnapshot Snapshot,
        DocumentQueueItem? QueueItem);

    #endregion
}
