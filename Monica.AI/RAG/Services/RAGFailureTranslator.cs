using System.Net.Sockets;
using Microsoft.Extensions.VectorData;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Translates low-level RAG infrastructure failures into user-facing messages.
/// </summary>
public static class RAGFailureTranslator
{
    private const string VectorStoreRecoveryHint =
        "Check the configured vector store service and its host/port configuration, then try again.";

    private static string IndexingStartMessage =>
        $"Cannot start indexing because the configured vector store is unavailable or misconfigured. {VectorStoreRecoveryHint}";

    private static string DocumentIndexingMessage =>
        $"Document indexing failed because the configured vector store is unavailable or misconfigured. {VectorStoreRecoveryHint}";

    private static string EmbeddingModelSwitchMessage =>
        $"Cannot switch the embedding model because the existing vector index could not be cleared. {VectorStoreRecoveryHint}";

    private static string RagSupportRemovalMessage =>
        $"Cannot remove RAG support because the configured vector store is unavailable or misconfigured. You can force local removal if you accept that stale vectors may remain in the vector store. {VectorStoreRecoveryHint}";

    private static string VectorCollectionOverwriteMessage =>
        $"Cannot overwrite the existing vector collection because the configured vector store is unavailable or misconfigured. {VectorStoreRecoveryHint}";

    /// <summary>
    /// Returns a friendly message for failures that block indexing from starting.
    /// </summary>
    public static string DescribeIndexingStart(Exception exception)
    {
        return IsVectorStoreFailure(exception)
            ? IndexingStartMessage
            : exception.Message;
    }

    /// <summary>
    /// Returns a friendly message for failures raised while indexing one document.
    /// </summary>
    public static string DescribeDocumentIndexing(Exception exception)
    {
        return IsVectorStoreFailure(exception)
            ? DocumentIndexingMessage
            : exception.Message;
    }

    /// <summary>
    /// Returns a friendly message for failures raised while switching embedding models.
    /// </summary>
    public static string DescribeEmbeddingModelSwitch(Exception exception)
    {
        return IsVectorStoreFailure(exception)
            ? EmbeddingModelSwitchMessage
            : exception.Message;
    }

    /// <summary>
    /// Returns a friendly message for failures raised while removing RAG support.
    /// </summary>
    public static string DescribeRagSupportRemoval(Exception exception)
    {
        return IsVectorStoreFailure(exception)
            ? RagSupportRemovalMessage
            : exception.Message;
    }

    /// <summary>
    /// Returns a friendly message for failures raised while clearing an existing vector collection for reuse.
    /// </summary>
    public static string DescribeVectorCollectionOverwrite(Exception exception)
    {
        return IsVectorStoreFailure(exception)
            ? VectorCollectionOverwriteMessage
            : exception.Message;
    }

    /// <summary>
    /// Normalizes persisted document-level errors into the same friendly message used for new failures.
    /// </summary>
    public static string? NormalizeDocumentErrorMessage(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return errorMessage;
        }

        return MatchesVectorStoreFailure(errorMessage)
            ? DocumentIndexingMessage
            : errorMessage;
    }

    /// <summary>
    /// Determines whether the exception chain indicates a vector store connectivity or configuration failure.
    /// </summary>
    public static bool IsVectorStoreFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        foreach (var current in Enumerate(exception))
        {
            if (current is SocketException or VectorStoreException)
            {
                return true;
            }

            if (string.Equals(current.GetType().FullName, "Grpc.Core.RpcException", StringComparison.Ordinal))
            {
                return true;
            }

            if (MatchesVectorStoreFailure(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Exception> Enumerate(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static bool MatchesVectorStoreFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("Call to vector store failed", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Error connecting to subchannel", StringComparison.OrdinalIgnoreCase)
               || message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase)
               || message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
               || message.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
               || message.Contains("StatusCode=\"Unavailable\"", StringComparison.OrdinalIgnoreCase);
    }
}
