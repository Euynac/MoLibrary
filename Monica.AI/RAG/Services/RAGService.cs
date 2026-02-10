using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;
using Monica.AI.Modules;
using Monica.AI.Services;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Core RAG service - orchestrates indexing and retrieval.
/// Infrastructure service: uses exceptions, not Res&lt;T&gt;.
///
/// Uses VectorStoreCollectionDefinition (dynamic schema) and
/// search adapter delegate pattern from agent-framework.
/// </summary>
public class RAGService(
    IKnowledgeBaseStore kbStore,
    VectorStore vectorStore,
    IAIProviderFactory providerFactory,
    AIModelCatalog modelCatalog,
    IEnumerable<IDocumentChunker> chunkers,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGService> logger)
{
    private readonly ModuleRAGOption _options = options.Value;
    private readonly IReadOnlyList<IDocumentChunker> _chunkers = chunkers.ToList();

    private readonly VectorStoreCollectionDefinition _collectionDefinition =
        CreateCollectionDefinition(
            ResolveVectorDimensions(options.Value, providerFactory, modelCatalog));

    private readonly ConcurrentDictionary<string, VectorStoreCollection<string, Dictionary<string, object?>>> _collections = new();

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
        await collection.EnsureCollectionExistsAsync(ct);

        var embeddingGenerator = GetEmbeddingGenerator();
        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var record = new Dictionary<string, object?>
            {
                ["Key"] = $"{knowledgeBaseId}_{documentPath}_{chunk.ChunkIndex}",
                ["KnowledgeBaseId"] = knowledgeBaseId,
                ["DocumentPath"] = documentPath,
                ["DocumentTitle"] = documentTitle,
                ["Content"] = chunk.Content,
                ["SectionPath"] = chunk.SectionPath,
                ["ChunkIndex"] = chunk.ChunkIndex,
                ["ContentEmbedding"] = embeddings[i].Vector
            };

            await collection.UpsertAsync(record, ct);

            progress?.Report(new IndexingProgress(i + 1, chunks.Count, documentTitle));
        }

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
        int topK = 5,
        CancellationToken ct = default)
    {
        var results = new List<TextSearchResult>();

        foreach (var kbId in knowledgeBaseIds)
        {
            var collectionName = GetCollectionName(kbId);
            var collection = GetOrCreateCollection(collectionName);

            var collectionExists = await collection.CollectionExistsAsync(ct);
            if (!collectionExists) continue;

            var searchResults = collection.SearchAsync<string>(
                query,
                top: topK,
                cancellationToken: ct);

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
    /// Creates a search adapter delegate for use with RAGContextProvider.
    /// </summary>
    public Func<string, CancellationToken, Task<IEnumerable<TextSearchResult>>>
        CreateSearchAdapter(IEnumerable<string> knowledgeBaseIds, int topK = 5)
    {
        var kbIds = knowledgeBaseIds.ToList();
        return async (query, ct) => await SearchAsync(query, kbIds, topK, ct);
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

    private string GetCollectionName(string knowledgeBaseId)
        => $"{_options.CollectionNamePrefix}{knowledgeBaseId}";

    private VectorStoreCollection<string, Dictionary<string, object?>> GetOrCreateCollection(string collectionName)
    {
        return _collections.GetOrAdd(collectionName, name =>
            vectorStore.GetCollection<string, Dictionary<string, object?>>(name, _collectionDefinition));
    }

    private IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator()
    {
        var provider = !string.IsNullOrWhiteSpace(_options.EmbeddingProviderId)
            ? providerFactory.GetProvider(_options.EmbeddingProviderId)
              ?? throw new InvalidOperationException(
                  $"Embedding provider '{_options.EmbeddingProviderId}' not found.")
            : providerFactory.GetDefaultProvider()
              ?? throw new InvalidOperationException("No default AI provider configured.");

        return provider.GetEmbeddingGenerator(_options.EmbeddingModelName);
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
                new VectorStoreVectorProperty("ContentEmbedding", typeof(ReadOnlyMemory<float>), vectorDimensions),
            ]
        };

    private static int ResolveVectorDimensions(
        ModuleRAGOption ragOptions,
        IAIProviderFactory providerFactory,
        AIModelCatalog modelCatalog)
    {
        if (ragOptions.VectorDimensions.HasValue)
            return ragOptions.VectorDimensions.Value;

        var provider = !string.IsNullOrWhiteSpace(ragOptions.EmbeddingProviderId)
            ? providerFactory.GetProvider(ragOptions.EmbeddingProviderId)
            : providerFactory.GetDefaultProvider();

        var modelName = ragOptions.EmbeddingModelName
            ?? provider?.Info.SupportedModels?
                .OfType<EmbeddingModelInfo>()
                .FirstOrDefault()?.ModelName;

        if (string.IsNullOrWhiteSpace(modelName))
            throw new InvalidOperationException(
                "Cannot determine embedding model. Configure an embedding model " +
                "in the provider's SupportedModels or set VectorDimensions explicitly.");

        var modelInfo = modelCatalog.GetModel(modelName) as EmbeddingModelInfo
            ?? throw new InvalidOperationException(
                $"Embedding model '{modelName}' not found in catalog. " +
                "Register it via AddModel() or set VectorDimensions explicitly.");

        return modelInfo.Dimensions;
    }
}
