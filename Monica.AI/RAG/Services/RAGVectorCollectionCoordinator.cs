using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Monica.Modules;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Manages vector collection lifecycle and cached collection bindings.
/// </summary>
public sealed class RAGVectorCollectionCoordinator(
    VectorStore vectorStore,
    RAGEmbeddingBindingResolver embeddingBindingResolver,
    IEnumerable<RAGVectorStoreRegistrationInfo> vectorStoreRegistrations,
    IOptions<ModuleRAGOption> options,
    ILogger<RAGVectorCollectionCoordinator> logger)
{
    private readonly ModuleRAGOption _options = options.Value;
    private readonly RAGVectorStoreRegistrationInfo? _registrationInfo = vectorStoreRegistrations.FirstOrDefault();

    private readonly ConcurrentDictionary<string, VectorStoreCollection<Guid, RAGVectorRecord>> _collections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _collectionBindings =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _initializedCollections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public static string BuildRecordKey(string knowledgeBaseId, string documentPath, int chunkIndex)
        => RAGVectorRecord.BuildLogicalKey(knowledgeBaseId, documentPath, chunkIndex);

    public static Guid BuildStorageKey(string knowledgeBaseId, string documentPath, int chunkIndex)
        => RAGVectorRecord.BuildStorageKey(knowledgeBaseId, documentPath, chunkIndex);

    public string GetCollectionName(string knowledgeBaseId)
        => $"{_options.CollectionNamePrefix}{knowledgeBaseId}";

    /// <summary>
    /// Checks whether the vector collection assigned to a knowledge base currently exists.
    /// </summary>
    public async Task<KnowledgeBaseVectorCollectionStatus> GetKnowledgeBaseVectorCollectionStatusAsync(
        string knowledgeBaseId,
        CancellationToken ct)
    {
        var collectionName = GetCollectionName(knowledgeBaseId);
        try
        {
            var exists = await vectorStore.CollectionExistsAsync(collectionName, ct);
            return new KnowledgeBaseVectorCollectionStatus
            {
                KnowledgeBaseId = knowledgeBaseId,
                CollectionName = collectionName,
                WasChecked = true,
                Exists = exists
            };
        }
        catch (Exception ex) when (RAGFailureTranslator.IsVectorStoreFailure(ex))
        {
            logger.LogWarning(
                ex,
                "Failed to check vector collection '{CollectionName}' for KB '{KbId}'.",
                collectionName,
                knowledgeBaseId);
            return new KnowledgeBaseVectorCollectionStatus
            {
                KnowledgeBaseId = knowledgeBaseId,
                CollectionName = collectionName,
                WasChecked = false,
                Exists = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets runtime diagnostics for the configured vector store without opening a network connection.
    /// </summary>
    public VectorStoreDiagnosticInfo GetVectorStoreDiagnostics()
    {
        var metadata = vectorStore.GetService(typeof(VectorStoreMetadata)) as VectorStoreMetadata;
        var registrationInfo = _registrationInfo ?? new RAGVectorStoreRegistrationInfo
        {
            ProviderKind = "Unknown",
            ProviderDisplayName = vectorStore.GetType().Name
        };

        return new VectorStoreDiagnosticInfo
        {
            ProviderKind = registrationInfo.ProviderKind,
            ProviderDisplayName = registrationInfo.ProviderDisplayName,
            RuntimeTypeName = vectorStore.GetType().FullName ?? vectorStore.GetType().Name,
            VectorStoreSystemName = metadata?.VectorStoreSystemName,
            VectorStoreName = metadata?.VectorStoreName,
            CollectionNamePrefix = _options.CollectionNamePrefix,
            ConfigurationEntries = registrationInfo.ConfigurationEntries
        };
    }

    /// <summary>
    /// Runs a non-destructive vector-store connectivity probe through the registered provider.
    /// </summary>
    public async Task<VectorStoreConnectionTestResult> TestVectorStoreConnectionAsync(CancellationToken ct)
    {
        var probeCollectionName = $"{_options.CollectionNamePrefix}diagnostics_probe";
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var exists = await vectorStore.CollectionExistsAsync(probeCollectionName, ct);
            return new VectorStoreConnectionTestResult
            {
                Succeeded = true,
                ProbeCollectionName = probeCollectionName,
                ProbeCollectionExists = exists,
                Duration = Stopwatch.GetElapsedTime(startedAt),
                Message = $"Connection succeeded. Probe collection exists: {exists}."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vector store connectivity probe failed for collection '{CollectionName}'.", probeCollectionName);
            return new VectorStoreConnectionTestResult
            {
                Succeeded = false,
                ProbeCollectionName = probeCollectionName,
                Duration = Stopwatch.GetElapsedTime(startedAt),
                Message = ex.Message
            };
        }
    }

    public async Task<VectorStoreCollection<Guid, RAGVectorRecord>> GetOrCreateCollectionAsync(
        KnowledgeBaseModel kb,
        RAGEmbeddingBinding binding,
        CancellationToken ct)
        => await GetCollectionCoreAsync(kb, binding, ensureCollectionExists: true, ct);

    public async Task<VectorStoreCollection<Guid, RAGVectorRecord>> GetCollectionAsync(
        KnowledgeBaseModel kb,
        RAGEmbeddingBinding binding,
        CancellationToken ct)
        => await GetCollectionCoreAsync(kb, binding, ensureCollectionExists: false, ct);

    public async Task<IReadOnlyList<string>> GetMissingRecordKeysAsync(
        KnowledgeBaseModel kb,
        RAGEmbeddingBinding binding,
        IEnumerable<string> recordKeys,
        CancellationToken ct)
    {
        var keys = recordKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            return [];
        }

        var collection = await GetCollectionCoreAsync(kb, binding, ensureCollectionExists: false, ct);
        if (!await collection.CollectionExistsAsync(ct))
        {
            return keys;
        }

        var foundKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var record in collection.GetAsync(
                           keys.Select(RAGVectorRecord.BuildStorageKey),
                           new RecordRetrievalOptions { IncludeVectors = false },
                           ct))
        {
            if (!string.IsNullOrWhiteSpace(record.LogicalKey))
            {
                foundKeys.Add(record.LogicalKey);
            }
        }

        return keys
            .Where(key => !foundKeys.Contains(key))
            .ToList();
    }

    private async Task<VectorStoreCollection<Guid, RAGVectorRecord>> GetCollectionCoreAsync(
        KnowledgeBaseModel kb,
        RAGEmbeddingBinding binding,
        bool ensureCollectionExists,
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
            return vectorStore.GetCollection<Guid, RAGVectorRecord>(collectionName, definition);
        });

        _collectionBindings[collectionName] = bindingKey;
        if (ensureCollectionExists)
        {
            await EnsureCollectionInitializedAsync(collectionName, collection, ct);
        }

        return collection;
    }

    public async Task<int> RemoveIndexedDocumentDataAsync(
        KnowledgeBaseModel kb,
        DocumentIndexState? existingState,
        CancellationToken ct)
    {
        if (existingState is null)
        {
            return 0;
        }

        VectorStoreCollection<Guid, RAGVectorRecord>? collection = null;
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
            .Select(index => BuildStorageKey(kb.Id, existingState.DocumentPath, index))
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
        VectorStoreCollection<Guid, RAGVectorRecord> collection,
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
                new VectorStoreKeyProperty(nameof(RAGVectorRecord.StorageKey), typeof(Guid)),
                new VectorStoreDataProperty(nameof(RAGVectorRecord.LogicalKey), typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(RAGVectorRecord.KnowledgeBaseId), typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(RAGVectorRecord.DocumentPath), typeof(string)) { IsIndexed = true },
                new VectorStoreDataProperty(nameof(RAGVectorRecord.DocumentTitle), typeof(string)),
                new VectorStoreDataProperty(nameof(RAGVectorRecord.Content), typeof(string)) { IsFullTextIndexed = true },
                new VectorStoreDataProperty(nameof(RAGVectorRecord.SectionPath), typeof(string)),
                new VectorStoreDataProperty(nameof(RAGVectorRecord.ChunkIndex), typeof(int)),
                new VectorStoreDataProperty(nameof(RAGVectorRecord.ChunkStart), typeof(int)),
                new VectorStoreDataProperty(nameof(RAGVectorRecord.ChunkEnd), typeof(int)),
                new VectorStoreDataProperty(nameof(RAGVectorRecord.ChunkerId), typeof(string)),
                new VectorStoreVectorProperty(nameof(RAGVectorRecord.ContentEmbedding), typeof(float[]), vectorDimensions)
                {
                    EmbeddingGenerator = embeddingGenerator
                }
            ]
        };
    }
}
