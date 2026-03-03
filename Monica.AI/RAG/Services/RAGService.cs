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
    VectorStore vectorStore,
    IAIProviderFactory providerFactory,
    IEnumerable<IDocumentChunker> chunkers,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGService> logger)
{
    private readonly ModuleRAGOption _options = options.Value;
    private readonly IReadOnlyList<IDocumentChunker> _chunkers = chunkers.ToList();

    private readonly ConcurrentDictionary<string, VectorStoreCollection<object, Dictionary<string, object?>>> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _collectionBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _initializedCollections = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task<KnowledgeBase> CreateKnowledgeBaseAsync(
        string name, string? description = null,
        string? searchToolDescription = null,
        CancellationToken ct = default)
    {
        var kb = new KnowledgeBase
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
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
        await kbStore.DeleteAsync(knowledgeBaseId, ct);
        logger.LogInformation("Deleted knowledge base {Id}", knowledgeBaseId);
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

        var extension = Path.GetExtension(documentPath);
        var chunker = _chunkers.FirstOrDefault(c =>
            c.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            ?? throw new NotSupportedException(
                $"No chunker registered for extension '{extension}'.");

        var chunks = chunker.ChunkDocument(content, documentPath, documentTitle);
        if (chunks.Count == 0) return;

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

        // Build all records with pre-generated vectors.
        var records = chunks.Select((chunk, index) => new Dictionary<string, object?>
            {
                ["Key"] = $"{knowledgeBaseId}_{documentPath}_{chunk.ChunkIndex}",
                ["KnowledgeBaseId"] = knowledgeBaseId,
                ["DocumentPath"] = documentPath,
                ["DocumentTitle"] = documentTitle,
                ["Content"] = chunk.Content,
                ["SectionPath"] = chunk.SectionPath,
                ["ChunkIndex"] = chunk.ChunkIndex,
                ["ContentEmbedding"] = vectors[index]
            })
            .ToList();

        await collection.UpsertAsync(records, ct);

        progress?.Report(new IndexingProgress(chunks.Count, chunks.Count, documentTitle));

        kb.DocumentCount++;
        kb.ChunkCount += chunks.Count;
        await kbStore.SaveAsync(kb, ct);

        logger.LogInformation(
            "Indexed document '{Title}' into KB '{KbId}': {ChunkCount} chunks",
            documentTitle, knowledgeBaseId, chunks.Count);
    }

    public async Task<IReadOnlyList<TextSearchResult>> SearchAsync(
        string query,
        IEnumerable<string> knowledgeBaseIds,
        int topK = 0,
        CancellationToken ct = default)
    {
        if (topK <= 0) topK = _options.DefaultTopK;
        var results = new List<TextSearchResult>();

        foreach (var kbId in knowledgeBaseIds)
        {
            var kb = await kbStore.GetByIdAsync(kbId, ct);
            if (kb is null)
            {
                continue;
            }

            var collection = await GetOrCreateCollectionForKnowledgeBaseAsync(kb, ct);

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

    #region Private helpers

    private static string BuildBindingKey(string providerId, string modelName, int dimensions)
        => $"{providerId}::{modelName}::{dimensions}";

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
                new VectorStoreVectorProperty("ContentEmbedding", typeof(float[]), vectorDimensions)
                {
                    EmbeddingGenerator = embeddingGenerator
                }
            ]
        };
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

    [GeneratedRegex(@"\p{L}+", RegexOptions.IgnoreCase)]
    private static partial Regex WordSegmenter();

    private readonly record struct EmbeddingBinding(string ProviderId, string ModelName, int Dimensions);

    #endregion
}
