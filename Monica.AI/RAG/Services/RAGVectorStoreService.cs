using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Microsoft.Extensions.Logging;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Owns embedding bindings, vector collection lifecycle, diagnostics, and validation.
/// </summary>
internal sealed class RAGVectorStoreService(
    IDocumentIndexStateStore indexStateStore,
    RAGEmbeddingBindingResolver embeddingBindings,
    RAGVectorCollectionCoordinator vectorCollections,
    RAGIndexStateCoordinator indexStates,
    RAGIndexingActivity indexingActivity,
    ILogger<RAGVectorStoreService> logger)
{
    /// <summary>Persists an embedding binding and optionally clears incompatible vector data.</summary>
    public async Task SetEmbeddingModelAsync(
        string knowledgeBaseId,
        string providerId,
        string modelName,
        bool clearIndex = true,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var normalizedProviderId = providerId.Trim();
        var normalizedModelName = modelName.Trim();
        _ = await embeddingBindings.ResolveAsync(
            knowledgeBase with
            {
                EmbeddingProviderId = normalizedProviderId,
                EmbeddingModelName = normalizedModelName
            },
            ct);

        var changed = !string.Equals(
                          knowledgeBase.EmbeddingProviderId,
                          normalizedProviderId,
                          StringComparison.OrdinalIgnoreCase)
                      || !string.Equals(
                          knowledgeBase.EmbeddingModelName,
                          normalizedModelName,
                          StringComparison.OrdinalIgnoreCase);
        if (changed && clearIndex && await HasIndexedContentAsync(knowledgeBase, ct))
        {
            try
            {
                await vectorCollections.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
                await indexStates.ResetKnowledgeBaseDocumentStatesForReindexAsync(knowledgeBaseId, ct);
                await indexStates.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);
            }
            catch (Exception ex) when (RAGFailureTranslator.IsVectorStoreFailure(ex))
            {
                throw new InvalidOperationException(RAGFailureTranslator.DescribeEmbeddingModelSwitch(ex), ex);
            }
        }

        knowledgeBase.EmbeddingProviderId = normalizedProviderId;
        knowledgeBase.EmbeddingModelName = normalizedModelName;
        await indexStateStore.UpsertKnowledgeBaseAsync(knowledgeBase, ct);
    }

    /// <summary>Removes RAG support while preserving source documents.</summary>
    public async Task<KnowledgeBaseRagSupportRemovalResult> RemoveSupportAsync(
        string knowledgeBaseId,
        bool forceLocalMetadataRemoval = false,
        CancellationToken ct = default)
    {
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        _ = await ConvergeInactiveAsync(knowledgeBaseId, ct);
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        EnsureNoActiveIndexing(states, "remove RAG support");

        var indexedDocumentCount = states.Count(static state =>
            state.Status == DocumentStatus.Done || state.ChunkCount > 0);
        var chunkCount = states.Sum(static state => Math.Max(0, state.ChunkCount));
        var collectionName = vectorCollections.GetCollectionName(knowledgeBaseId);
        if (!forceLocalMetadataRemoval)
        {
            await vectorCollections.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
        }
        else
        {
            logger.LogWarning(
                "Removing local RAG metadata for '{KnowledgeBaseId}' without deleting vector collection '{CollectionName}'.",
                knowledgeBaseId,
                collectionName);
        }

        var resetCount = await indexStates.ResetIndexedDocumentStatesToPendingAsync(knowledgeBaseId, ct);
        knowledgeBase.EmbeddingProviderId = null;
        knowledgeBase.EmbeddingModelName = null;
        await indexStates.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);
        await indexStateStore.UpsertKnowledgeBaseAsync(knowledgeBase, ct);

        return new KnowledgeBaseRagSupportRemovalResult
        {
            KnowledgeBaseId = knowledgeBaseId,
            ResetDocumentCount = resetCount,
            ClearedIndexedDocumentCount = indexedDocumentCount,
            ClearedChunkCount = forceLocalMetadataRemoval ? 0 : chunkCount,
            VectorCollectionName = collectionName,
            VectorStoreCleanupSucceeded = !forceLocalMetadataRemoval,
            WasForced = forceLocalMetadataRemoval,
            StaleVectorCollectionMayRemain = forceLocalMetadataRemoval
        };
    }

    /// <summary>Ensures the embedding model and vector collection are ready for indexing.</summary>
    public async Task EnsureIndexingReadyAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var binding = await embeddingBindings.ResolveAsync(knowledgeBase, ct);
        try
        {
            _ = await vectorCollections.GetOrCreateCollectionAsync(knowledgeBase, binding, ct);
        }
        catch (Exception ex) when (RAGFailureTranslator.IsVectorStoreFailure(ex))
        {
            throw new InvalidOperationException(RAGFailureTranslator.DescribeIndexingStart(ex), ex);
        }
    }

    /// <summary>Returns configured vector-store diagnostics.</summary>
    public VectorStoreDiagnosticInfo GetDiagnostics() => vectorCollections.GetVectorStoreDiagnostics();

    /// <summary>Runs a non-destructive vector-store connection probe.</summary>
    public Task<VectorStoreConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
        => vectorCollections.TestVectorStoreConnectionAsync(ct);

    /// <summary>Returns vector collection existence status.</summary>
    public Task<KnowledgeBaseVectorCollectionStatus> GetCollectionStatusAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
        => vectorCollections.GetKnowledgeBaseVectorCollectionStatusAsync(knowledgeBaseId, ct);

    /// <summary>Clears and resets a knowledge base's vector collection.</summary>
    public async Task<KnowledgeBaseVectorCollectionOverwriteResult> OverwriteCollectionAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        _ = await ConvergeInactiveAsync(knowledgeBaseId, ct);
        var states = await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct);
        EnsureNoActiveIndexing(states, "overwrite the vector collection");
        var clearedChunkCount = states.Sum(static state => Math.Max(0, state.ChunkCount));
        var collectionName = vectorCollections.GetCollectionName(knowledgeBaseId);

        await vectorCollections.ClearCollectionCacheAndStorageAsync(knowledgeBaseId, ct);
        var resetCount = await indexStates.ResetIndexedDocumentStatesToPendingAsync(knowledgeBaseId, ct);
        await indexStates.RefreshKnowledgeBaseStatsAsync(knowledgeBase, ct);

        return new KnowledgeBaseVectorCollectionOverwriteResult
        {
            KnowledgeBaseId = knowledgeBaseId,
            CollectionName = collectionName,
            ResetDocumentCount = resetCount,
            ClearedChunkCount = clearedChunkCount
        };
    }

    /// <summary>Validates representative persisted vector keys for indexed documents.</summary>
    public async Task<KnowledgeBaseVectorValidationResult> ValidateAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        var knowledgeBase = await indexStates.GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        _ = await ConvergeInactiveAsync(knowledgeBaseId, ct);
        var indexedStates = (await indexStateStore.GetDocumentStatesAsync(knowledgeBaseId, ct))
            .Where(static state => state.Status == DocumentStatus.Done && state.ChunkCount > 0)
            .OrderBy(static state => state.DocumentPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (indexedStates.Count == 0)
        {
            return EmptyValidation(knowledgeBaseId);
        }

        if (string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingProviderId)
            || string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingModelName))
        {
            return UnavailableValidation(knowledgeBaseId, indexedStates);
        }

        RAGEmbeddingBinding binding;
        try
        {
            binding = await embeddingBindings.ResolveAsync(knowledgeBase, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to resolve vector validation binding for '{KnowledgeBaseId}'.", knowledgeBaseId);
            return UnavailableValidation(knowledgeBaseId, indexedStates);
        }

        var expectedKeys = BuildValidationKeys(indexedStates, knowledgeBaseId);
        var missingKeys = await vectorCollections.GetMissingRecordKeysAsync(
            knowledgeBase,
            binding,
            expectedKeys.Keys,
            ct);
        return new KnowledgeBaseVectorValidationResult
        {
            KnowledgeBaseId = knowledgeBaseId,
            HasPersistedIndexMetadata = true,
            WasValidated = true,
            HasValidVectors = missingKeys.Count == 0,
            IndexedDocumentCount = indexedStates.Count,
            IndexedChunkCount = indexedStates.Sum(static state => state.ChunkCount),
            MissingDocumentPath = missingKeys
                .Select(key => expectedKeys.GetValueOrDefault(key))
                .FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path)),
            MissingVectorRecordKeys = missingKeys
        };
    }

    private async Task<int> ConvergeInactiveAsync(string knowledgeBaseId, CancellationToken ct)
        => await indexStates.ConvergeInactiveIndexingDocumentsAsync(
            knowledgeBaseId,
            indexingActivity.IsActive,
            ct);

    private void EnsureNoActiveIndexing(IEnumerable<DocumentIndexState> states, string operation)
    {
        if (states.Any(state =>
                state.Status == DocumentStatus.Indexing
                && indexingActivity.IsActive(state.KnowledgeBaseId, state.DocumentPath)))
        {
            throw new InvalidOperationException($"Cannot {operation} while indexing is in progress.");
        }
    }

    private async Task<bool> HasIndexedContentAsync(KnowledgeBaseModel knowledgeBase, CancellationToken ct)
        => knowledgeBase.ChunkCount > 0
           || (await indexStateStore.GetDocumentStatesAsync(knowledgeBase.Id, ct))
           .Any(static state => state.Status == DocumentStatus.Done || state.ChunkCount > 0);

    private static Dictionary<string, string> BuildValidationKeys(
        IEnumerable<DocumentIndexState> states,
        string knowledgeBaseId)
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in states)
        {
            keys[RAGVectorCollectionCoordinator.BuildRecordKey(knowledgeBaseId, state.DocumentPath, 0)] = state.DocumentPath;
            if (state.ChunkCount > 1)
            {
                keys[RAGVectorCollectionCoordinator.BuildRecordKey(
                    knowledgeBaseId,
                    state.DocumentPath,
                    state.ChunkCount - 1)] = state.DocumentPath;
            }
        }

        return keys;
    }

    private static KnowledgeBaseVectorValidationResult EmptyValidation(string knowledgeBaseId)
        => new()
        {
            KnowledgeBaseId = knowledgeBaseId,
            HasPersistedIndexMetadata = false,
            WasValidated = true,
            HasValidVectors = true,
            IndexedDocumentCount = 0,
            IndexedChunkCount = 0
        };

    private static KnowledgeBaseVectorValidationResult UnavailableValidation(
        string knowledgeBaseId,
        IReadOnlyCollection<DocumentIndexState> states)
        => new()
        {
            KnowledgeBaseId = knowledgeBaseId,
            HasPersistedIndexMetadata = true,
            WasValidated = false,
            HasValidVectors = false,
            IndexedDocumentCount = states.Count,
            IndexedChunkCount = states.Sum(static state => state.ChunkCount)
        };
}
