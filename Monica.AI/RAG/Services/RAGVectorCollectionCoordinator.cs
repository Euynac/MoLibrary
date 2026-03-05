using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Monica.AI.Models;
using Monica.AI.Modules;
using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Manages vector collection lifecycle and cached collection bindings.
/// </summary>
public sealed class RAGVectorCollectionCoordinator(
    VectorStore vectorStore,
    RAGEmbeddingBindingResolver embeddingBindingResolver,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGVectorCollectionCoordinator> logger)
{
    private readonly ModuleRAGOption _options = options.Value;

    private readonly ConcurrentDictionary<string, VectorStoreCollection<object, Dictionary<string, object?>>> _collections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _collectionBindings =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _initializedCollections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public static string BuildRecordKey(string knowledgeBaseId, string documentPath, int chunkIndex)
        => $"{knowledgeBaseId}_{documentPath}_{chunkIndex}";

    public string GetCollectionName(string knowledgeBaseId)
        => $"{_options.CollectionNamePrefix}{knowledgeBaseId}";

    public async Task<VectorStoreCollection<object, Dictionary<string, object?>>> GetOrCreateCollectionAsync(
        KnowledgeBase kb,
        RAGEmbeddingBinding binding,
        CancellationToken ct)
    {
        var collectionName = GetCollectionName(kb.Id);
        var bindingKey = BuildBindingKey(binding.ProviderId, binding.ModelName, binding.Dimensions);

        if (_collectionBindings.TryGetValue(collectionName, out var existingBindingKey)
            && !string.Equals(existingBindingKey, bindingKey, StringComparison.OrdinalIgnoreCase))
        {
            await ClearCollectionCacheAndStorageByNameAsync(collectionName, ct);
        }

        var collection = _collections.GetOrAdd(collectionName, _ =>
        {
            var embeddingGenerator = embeddingBindingResolver.GetEmbeddingGenerator(binding);
            var definition = CreateCollectionDefinition(binding.Dimensions, embeddingGenerator);
            return vectorStore.GetDynamicCollection(collectionName, definition);
        });

        _collectionBindings[collectionName] = bindingKey;
        await EnsureCollectionInitializedAsync(collectionName, collection, ct);
        return collection;
    }

    public async Task<int> RemoveIndexedDocumentDataAsync(
        KnowledgeBase kb,
        DocumentIndexState? existingState,
        CancellationToken ct)
    {
        if (existingState is null)
        {
            return 0;
        }

        VectorStoreCollection<object, Dictionary<string, object?>>? collection = null;
        try
        {
            var binding = await embeddingBindingResolver.ResolveAsync(kb, ct);
            collection = await GetOrCreateCollectionAsync(kb, binding, ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Skip vector cleanup for document '{DocumentPath}' in KB '{KbId}' because collection cannot be resolved.",
                existingState.DocumentPath,
                kb.Id);
        }

        if (collection is null || !await collection.CollectionExistsAsync(ct))
        {
            return 0;
        }

        if (existingState.ChunkCount <= 0)
        {
            return 0;
        }

        var keys = Enumerable.Range(0, existingState.ChunkCount)
            .Select(index => (object)BuildRecordKey(kb.Id, existingState.DocumentPath, index))
            .ToList();

        await collection.DeleteAsync(keys, ct);
        return existingState.ChunkCount;
    }

    public Task ClearCollectionCacheAndStorageAsync(string knowledgeBaseId, CancellationToken ct)
        => ClearCollectionCacheAndStorageByNameAsync(GetCollectionName(knowledgeBaseId), ct);

    private async Task ClearCollectionCacheAndStorageByNameAsync(string collectionName, CancellationToken ct)
    {
        await vectorStore.EnsureCollectionDeletedAsync(collectionName, ct);
        _collections.TryRemove(collectionName, out _);
        _collectionBindings.TryRemove(collectionName, out _);
        _initializedCollections.TryRemove(collectionName, out _);
    }

    private async Task EnsureCollectionInitializedAsync(
        string collectionName,
        VectorStoreCollection<object, Dictionary<string, object?>> collection,
        CancellationToken ct)
    {
        if (_initializedCollections.ContainsKey(collectionName))
        {
            return;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initializedCollections.ContainsKey(collectionName))
            {
                return;
            }

            await collection.EnsureCollectionExistsAsync(ct);
            _initializedCollections[collectionName] = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string BuildBindingKey(string providerId, string modelName, int dimensions)
        => $"{providerId}::{modelName}::{dimensions}";

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
}
