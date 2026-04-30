using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.AI.RAG.Services.Support;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.RAG.Facades;

/// <summary>
/// Host-facing facade for RAG operations.
/// </summary>
public class RAGFacade(
    IServiceProvider serviceProvider,
    ILogger<RAGFacade> logger)
{
    public const string CAN_FORCE_REMOVE_RAG_SUPPORT_METADATA_KEY = "canForceRemoveRagSupport";

    public async Task<Res<KnowledgeBaseVectorValidationResult>> GetKnowledgeBaseVectorValidationAsync(string kbId)
    {
        try
        {
            var result = await GetRagService().ValidateKnowledgeBaseVectorsAsync(kbId);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate vectors for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to validate vectors: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets runtime diagnostics for the configured vector store.
    /// </summary>
    public Task<Res<VectorStoreDiagnosticInfo>> GetVectorStoreDiagnosticsAsync()
    {
        try
        {
            var result = GetRagService().GetVectorStoreDiagnostics();
            return Task.FromResult(Res.Ok(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get vector store diagnostics.");
            return Task.FromResult<Res<VectorStoreDiagnosticInfo>>(
                Res.Fail($"Failed to load vector store diagnostics: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Runs a non-destructive connectivity test against the configured vector store.
    /// </summary>
    public async Task<Res<VectorStoreConnectionTestResult>> TestVectorStoreConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await GetRagService().TestVectorStoreConnectionAsync(ct);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test vector store connection.");
            return Res.Ok(new VectorStoreConnectionTestResult
            {
                Succeeded = false,
                ProbeCollectionName = string.Empty,
                Message = ex.GetMessageRecursively()
            });
        }
    }

    /// <summary>
    /// Removes RAG support from one knowledge base and clears its persisted vector/index data.
    /// </summary>
    /// <param name="kbId">Knowledge base identifier.</param>
    /// <param name="forceLocalMetadataRemoval">Whether local RAG metadata can be removed when remote vector cleanup fails.</param>
    public async Task<Res<KnowledgeBaseRagSupportRemovalResult>> RemoveKnowledgeBaseRagSupportAsync(
        string kbId,
        bool forceLocalMetadataRemoval = false)
    {
        try
        {
            var result = await GetRagService().RemoveKnowledgeBaseRagSupportAsync(kbId, forceLocalMetadataRemoval);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove RAG support for KB '{KbId}'", kbId);
            if (RAGFailureTranslator.IsVectorStoreFailure(ex))
            {
                return Res.Fail(RAGFailureTranslator.DescribeRagSupportRemoval(ex))
                    .AppendMetadata(CAN_FORCE_REMOVE_RAG_SUPPORT_METADATA_KEY, true);
            }

            return ex is InvalidOperationException or KeyNotFoundException
                ? Res.Fail(ex.GetMessageRecursively())
                : Res.Fail($"Failed to remove RAG support: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Checks whether the active vector store already contains the collection assigned to one knowledge base.
    /// </summary>
    public async Task<Res<KnowledgeBaseVectorCollectionStatus>> GetKnowledgeBaseVectorCollectionStatusAsync(string kbId)
    {
        try
        {
            var result = await GetRagService().GetKnowledgeBaseVectorCollectionStatusAsync(kbId);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check vector collection status for KB '{KbId}'", kbId);
            return ex is InvalidOperationException or KeyNotFoundException
                ? Res.Fail(ex.GetMessageRecursively())
                : Res.Fail($"Failed to check vector collection status: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Clears an existing vector collection so the knowledge base ID can be reused.
    /// </summary>
    public async Task<Res<KnowledgeBaseVectorCollectionOverwriteResult>> OverwriteKnowledgeBaseVectorCollectionAsync(string kbId)
    {
        try
        {
            var result = await GetRagService().OverwriteKnowledgeBaseVectorCollectionAsync(kbId);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to overwrite vector collection for KB '{KbId}'", kbId);
            if (RAGFailureTranslator.IsVectorStoreFailure(ex))
            {
                return Res.Fail(RAGFailureTranslator.DescribeVectorCollectionOverwrite(ex));
            }

            return ex is InvalidOperationException or KeyNotFoundException
                ? Res.Fail(ex.GetMessageRecursively())
                : Res.Fail($"Failed to overwrite vector collection: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res<IReadOnlyList<TextSearchResult>>> SearchAsync(
        string query,
        IEnumerable<string> kbIds,
        int topK = 5,
        RAGSearchEmbeddingOverride? embeddingOverride = null)
    {
        try
        {
            var results = await GetRagService().SearchAsync(
                query,
                kbIds,
                topK,
                embeddingOverride);
            return Res.Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query '{Query}'", query);
            return Res.Fail($"Search failed: {ex.GetMessageRecursively()}");
        }
    }

    #region Document Queue Management

    public async Task<Res> AddDocumentsToQueueAsync(
        string kbId,
        IEnumerable<string> documentIds,
        string? sourceGroupKey = null)
    {
        try
        {
            var docIdList = documentIds.ToList();
            logger.LogInformation("Adding {Count} documents to queue for KB '{KbId}'", docIdList.Count, kbId);

            var addedCount = await GetRagService().AddDocumentsToQueueAsync(
                kbId,
                docIdList,
                KnowledgeDocumentSourceKinds.Markdown,
                sourceGroupKey);

            return Res.Ok($"Added {addedCount} documents to queue.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add documents to queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to add documents: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res<int>> ClearDocumentQueueAsync(string kbId)
    {
        try
        {
            var removedCount = await GetRagService().ClearDocumentQueueAsync(kbId);
            return Res.Ok(removedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to clear queue: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res> ReindexDocumentAsync(
        string kbId,
        string documentId,
        IProgress<IndexingProgress>? progress = null)
    {
        try
        {
            logger.LogInformation("Reindexing document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            await GetRagService().QueueDocumentForReindexAsync(kbId, documentId);
            return Res.Ok("Document queued for reindexing.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reindex document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to reindex document: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res<int>> ReindexKnowledgeBaseAsync(string kbId)
    {
        try
        {
            logger.LogInformation("Queueing all documents in KB '{KbId}' for reindex", kbId);
            var queuedCount = await GetRagService().QueueKnowledgeBaseForReindexAsync(kbId);
            return Res.Ok(queuedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue KB '{KbId}' for reindex", kbId);
            return Res.Fail($"Failed to reindex knowledge base: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res> StartDocumentIndexingAsync(
        string kbId,
        string documentId,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetBatchIndexCoordinator().StartDocumentIndexingAsync(
                kbId,
                documentId,
                progress,
                cancellationToken);
            return Res.Ok("Document indexed successfully.");
        }
        catch (OperationCanceledException)
        {
            return Res.Fail("Request was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to start indexing for document '{DocumentId}' in KB '{KbId}'",
                documentId,
                kbId);
            return ex is InvalidOperationException or KeyNotFoundException
                ? Res.Fail(ex.GetMessageRecursively())
                : Res.Fail($"Failed to start document indexing: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Starts queue-based batch indexing.
    /// </summary>
    public Task<Res> StartBatchIndexingAsync(
        string kbId,
        int maxConcurrency = 5,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return StartBatchIndexingCoreAsync(kbId, maxConcurrency, progress, cancellationToken);
    }

    /// <summary>
    /// Starts queue-based batch indexing for selected document ids.
    /// </summary>
    public Task<Res> StartBatchIndexingAsync(
        string kbId,
        IEnumerable<string> documentIds,
        int maxConcurrency = 5,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return StartBatchIndexingCoreAsync(kbId, maxConcurrency, progress, cancellationToken, documentIds);
    }

    public Res CancelBatchIndexing(string kbId)
    {
        return GetBatchIndexCoordinator().TryCancelBatchIndexing(kbId, out var message)
            ? Res.Ok(string.IsNullOrWhiteSpace(message) ? "Cancellation requested." : message)
            : Res.Fail(message);
    }

    public async Task<Res> CancelBatchIndexingAsync(string kbId, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await GetBatchIndexCoordinator().CancelBatchIndexingAsync(kbId, cancellationToken);
            return Res.Ok(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel batch indexing for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to cancel batch indexing: {ex.GetMessageRecursively()}");
        }
    }

    public bool IsBatchIndexingActive(string kbId)
        => GetBatchIndexCoordinator().IsBatchIndexingActive(kbId);

    #endregion

    #region Chunk Viewer

    /// <summary>
    /// Gets document original text and chunk highlights for the chunk viewer.
    /// </summary>
    public async Task<Res<DocumentChunkView>> GetDocumentChunksAsync(
        string kbId,
        string documentId)
    {
        try
        {
            logger.LogInformation("Getting chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);

            var view = await GetChunkViewCoordinator().GetDocumentChunksAsync(kbId, documentId);
            return Res.Ok(view);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to load document chunks: {ex.GetMessageRecursively()}");
        }
    }

    #endregion

    private async Task<Res> StartBatchIndexingCoreAsync(
        string kbId,
        int maxConcurrency,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken,
        IEnumerable<string>? documentIds = null)
    {
        var outcome = await GetBatchIndexCoordinator().StartBatchIndexingAsync(
            kbId,
            maxConcurrency,
            progress,
            cancellationToken,
            documentIds);

        return outcome.Succeeded
            ? Res.Ok(outcome.Message)
            : Res.Fail(outcome.Message);
    }

    // Resolve heavy RAG graph only for methods that actually need it.
    private RAGService GetRagService()
        => serviceProvider.GetRequiredService<RAGService>();

    private BatchIndexCoordinator GetBatchIndexCoordinator()
        => serviceProvider.GetRequiredService<BatchIndexCoordinator>();

    private ChunkViewCoordinator GetChunkViewCoordinator()
        => serviceProvider.GetRequiredService<ChunkViewCoordinator>();
}
