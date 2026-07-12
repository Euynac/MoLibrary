using System.Collections.Concurrent;

namespace Monica.AI.RAG.Services.Support;

/// <summary>
/// Stores active batch indexing operations across dependency-injection scopes.
/// </summary>
internal sealed class RAGBatchIndexOperationRegistry
{
    private readonly ConcurrentDictionary<string, RAGBatchIndexOperation> _operations =
        new(StringComparer.OrdinalIgnoreCase);

    internal bool IsActive(string knowledgeBaseId) => _operations.ContainsKey(knowledgeBaseId);

    internal bool TryGet(string knowledgeBaseId, out RAGBatchIndexOperation? operation)
        => _operations.TryGetValue(knowledgeBaseId, out operation);

    internal bool TryStart(string knowledgeBaseId, out RAGBatchIndexOperation operation)
    {
        operation = new RAGBatchIndexOperation(knowledgeBaseId);
        if (_operations.TryAdd(knowledgeBaseId, operation))
        {
            return true;
        }

        operation.Dispose();
        return false;
    }

    internal void Complete(string knowledgeBaseId, RAGBatchIndexOperation operation)
    {
        if (_operations.TryGetValue(knowledgeBaseId, out var current)
            && ReferenceEquals(current, operation)
            && _operations.TryRemove(knowledgeBaseId, out var removed))
        {
            removed.Dispose();
        }
    }
}
