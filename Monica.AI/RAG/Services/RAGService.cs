using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.AI.Modules;
using AgentTextSearchResult = Microsoft.Agents.AI.TextSearchProvider.TextSearchResult;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Core RAG service — orchestrates document indexing and vector retrieval.
/// Infrastructure service: uses exceptions, not Res&lt;T&gt;.
///
/// Uses GetDynamicCollection (May 2025 API), auto-embedding via VectorStore,
/// batch upsert, hybrid search, and search adapter delegate for Phase 3
/// TextSearchProvider integration.
/// </summary>
public sealed partial class RAGService(
    IKnowledgeBaseStore kbStore,
    VectorStore vectorStore,
    IEnumerable<IDocumentChunker> chunkers,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGService> logger)
{
    private readonly ModuleRAGOption _options = options.Value;
    private readonly IReadOnlyList<IDocumentChunker> _chunkers = chunkers.ToList();

    private readonly VectorStoreCollectionDefinition _collectionDefinition =
        CreateCollectionDefinition(options.Value.VectorDimensions
            ?? throw new InvalidOperationException(
                "VectorDimensions must be configured. Set it explicitly in ModuleRAGOption " +
                "or use UseInMemoryVectorStore() which resolves it automatically."));

    private readonly ConcurrentDictionary<string, VectorStoreCollection<object, Dictionary<string, object?>>> _collections = new();
    private readonly ConcurrentDictionary<string, bool> _initializedCollections = new();
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

    public async Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var collectionName = GetCollectionName(knowledgeBaseId);
        var collection = GetOrCreateCollection(collectionName);

        await collection.EnsureCollectionDeletedAsync(ct);
        _collections.TryRemove(collectionName, out _);
        _initializedCollections.TryRemove(collectionName, out _);

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

        var collectionName = GetCollectionName(knowledgeBaseId);
        var collection = GetOrCreateCollection(collectionName);
        await EnsureCollectionInitializedAsync(collectionName, collection, ct);

        // Build all records — auto-embedding: pass text as ContentEmbedding value
        var records = chunks.Select(chunk => new Dictionary<string, object?>
        {
            ["Key"] = $"{knowledgeBaseId}_{documentPath}_{chunk.ChunkIndex}",
            ["KnowledgeBaseId"] = knowledgeBaseId,
            ["DocumentPath"] = documentPath,
            ["DocumentTitle"] = documentTitle,
            ["Content"] = chunk.Content,
            ["SectionPath"] = chunk.SectionPath,
            ["ChunkIndex"] = chunk.ChunkIndex,
            ["ContentEmbedding"] = chunk.Content // auto-embedded by VectorStore
        }).ToList();

        // Batch upsert — single round-trip
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
            var collectionName = GetCollectionName(kbId);
            var collection = GetOrCreateCollection(collectionName);

            if (!await collection.CollectionExistsAsync(ct)) continue;

            // Try hybrid search if available, otherwise vector-only
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
                    SectionPath = record["SectionPath"]?.ToString()
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
                RawRepresentation = r // preserve full metadata for custom formatters
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

    private string GetCollectionName(string knowledgeBaseId)
        => $"{_options.CollectionNamePrefix}{knowledgeBaseId}";

    private VectorStoreCollection<object, Dictionary<string, object?>> GetOrCreateCollection(string collectionName)
    {
        return _collections.GetOrAdd(collectionName, name =>
            vectorStore.GetDynamicCollection(name, _collectionDefinition));
    }

    /// <summary>
    /// Thread-safe collection initialization — EnsureCollectionExistsAsync called at most once per collection.
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

    private static VectorStoreCollectionDefinition CreateCollectionDefinition(int vectorDimensions)
        => new()
        {
            Properties =
            [
                new VectorStoreKeyProperty("Key", typeof(string)),
                new VectorStoreDataProperty("KnowledgeBaseId", typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty("DocumentPath", typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty("DocumentTitle", typeof(string)),
                new VectorStoreDataProperty("Content", typeof(string)) { IsFullTextIndexed = true },
                new VectorStoreDataProperty("SectionPath", typeof(string)),
                new VectorStoreDataProperty("ChunkIndex", typeof(int)),
                new VectorStoreVectorProperty("ContentEmbedding", typeof(string), vectorDimensions),
            ]
        };

    [GeneratedRegex(@"\p{L}+", RegexOptions.IgnoreCase)]
    private static partial Regex WordSegmenter();

    #endregion
}
