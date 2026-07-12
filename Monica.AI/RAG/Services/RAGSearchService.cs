using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.RAG.Models;
using Monica.Modules;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Executes semantic and hybrid retrieval over RAG vector collections.
/// </summary>
internal sealed partial class RAGSearchService(
    IKnowledgeBaseStore knowledgeBaseStore,
    RAGEmbeddingBindingResolver embeddingBindingResolver,
    RAGVectorCollectionCoordinator vectorCollections,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGSearchService> logger)
{
    private const int HYBRID_SIMILARITY_SEARCH_MULTIPLIER = 10;
    private const int HYBRID_SIMILARITY_SEARCH_MINIMUM = 100;
    private readonly ModuleRAGOption _options = options.Value;

    /// <summary>
    /// Searches selected knowledge bases using their configured embedding bindings.
    /// </summary>
    public async Task<IReadOnlyList<TextSearchResult>> SearchAsync(
        string query,
        IEnumerable<string> knowledgeBaseIds,
        int topK = 0,
        RAGSearchEmbeddingOverride? embeddingOverride = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var effectiveTopK = topK > 0 ? topK : _options.DefaultTopK;
        var results = new List<TextSearchResult>();
        var queryVectors = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        RAGEmbeddingBinding? overrideBinding = embeddingOverride is null
            ? null
            : await embeddingBindingResolver.ResolveAsync(
                embeddingOverride.ProviderId,
                embeddingOverride.ModelName,
                ct);

        foreach (var knowledgeBaseId in knowledgeBaseIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var knowledgeBase = await knowledgeBaseStore.GetKnowledgeBaseAsync(knowledgeBaseId, ct);
            if (knowledgeBase is null)
            {
                continue;
            }

            RAGEmbeddingBinding binding;
            try
            {
                binding = await embeddingBindingResolver.ResolveAsync(knowledgeBase, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Skipping knowledge base '{KnowledgeBaseId}' because its embedding binding is invalid.",
                    knowledgeBaseId);
                continue;
            }

            ValidateOverrideDimensions(overrideBinding, binding, knowledgeBase.Name);
            var collection = await vectorCollections.GetCollectionAsync(knowledgeBase, binding, ct);
            if (!await collection.CollectionExistsAsync(ct))
            {
                continue;
            }

            var searchBinding = overrideBinding ?? binding;
            var queryVector = await GetQueryVectorAsync(query, searchBinding, queryVectors, ct);
            var hybridSearch = collection.GetService(typeof(IKeywordHybridSearchable<RAGVectorRecord>))
                               as IKeywordHybridSearchable<RAGVectorRecord>;
            Dictionary<string, double> similarityScores = hybridSearch is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : await GetSimilarityScoresAsync(
                    collection,
                    knowledgeBaseId,
                    queryVector,
                    effectiveTopK,
                    ct);

            var searchResults = hybridSearch is null
                ? collection.SearchAsync(queryVector, top: effectiveTopK, cancellationToken: ct)
                : hybridSearch.HybridSearchAsync(
                    queryVector,
                    WordSegmenter().Matches(query).Select(static match => match.Value).ToList(),
                    top: effectiveTopK,
                    cancellationToken: ct);

            await foreach (var result in searchResults)
            {
                if (result.Record is not { } record)
                {
                    continue;
                }

                var logicalKey = RAGVectorCollectionCoordinator.BuildRecordKey(
                    knowledgeBaseId,
                    record.DocumentPath,
                    record.ChunkIndex);
                results.Add(new TextSearchResult
                {
                    SourceName = record.DocumentTitle,
                    SourceLink = record.DocumentPath,
                    Text = record.Content,
                    Score = result.Score,
                    ScoreKind = hybridSearch is null
                        ? TextSearchScoreKind.VectorSimilarity
                        : TextSearchScoreKind.HybridRank,
                    SimilarityScore = hybridSearch is null
                        ? result.Score
                        : similarityScores.TryGetValue(logicalKey, out var similarityScore)
                            ? similarityScore
                            : null,
                    KnowledgeBaseId = knowledgeBaseId,
                    SectionPath = record.SectionPath,
                    DocumentId = record.DocumentPath,
                    ChunkIndex = record.ChunkIndex
                });
            }
        }

        return results
            .OrderByDescending(static result => result.Score)
            .Take(effectiveTopK)
            .ToList();
    }

    private async Task<float[]> GetQueryVectorAsync(
        string query,
        RAGEmbeddingBinding binding,
        IDictionary<string, float[]> cache,
        CancellationToken ct)
    {
        var cacheKey = $"{binding.ProviderId}::{binding.ModelName}::{binding.Dimensions.ToString(CultureInfo.InvariantCulture)}";
        if (cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var generator = embeddingBindingResolver.GetEmbeddingGenerator(binding);
        var generated = await generator.GenerateAsync([query], cancellationToken: ct);
        var vector = generated.FirstOrDefault()?.Vector.ToArray()
                     ?? throw new InvalidOperationException(
                         $"Embedding generator returned no vector for '{binding.ProviderId}/{binding.ModelName}'.");
        cache[cacheKey] = vector;
        return vector;
    }

    private static async Task<Dictionary<string, double>> GetSimilarityScoresAsync(
        VectorStoreCollection<Guid, RAGVectorRecord> collection,
        string knowledgeBaseId,
        float[] queryVector,
        int topK,
        CancellationToken ct)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var similarityTop = Math.Max(
            topK * HYBRID_SIMILARITY_SEARCH_MULTIPLIER,
            HYBRID_SIMILARITY_SEARCH_MINIMUM);

        await foreach (var result in collection.SearchAsync(queryVector, top: similarityTop, cancellationToken: ct))
        {
            if (result.Record is not { } record || result.Score is not { } score)
            {
                continue;
            }

            scores[RAGVectorCollectionCoordinator.BuildRecordKey(
                knowledgeBaseId,
                record.DocumentPath,
                record.ChunkIndex)] = score;
        }

        return scores;
    }

    private static void ValidateOverrideDimensions(
        RAGEmbeddingBinding? overrideBinding,
        RAGEmbeddingBinding knowledgeBaseBinding,
        string knowledgeBaseName)
    {
        if (overrideBinding is null || overrideBinding.Value.Dimensions == knowledgeBaseBinding.Dimensions)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Search embedding model '{overrideBinding.Value.ModelName}' uses {overrideBinding.Value.Dimensions} dimensions, " +
            $"but knowledge base '{knowledgeBaseName}' uses {knowledgeBaseBinding.Dimensions}. Reindex or choose a compatible model.");
    }

    [GeneratedRegex(@"\p{L}+", RegexOptions.IgnoreCase)]
    private static partial Regex WordSegmenter();
}
