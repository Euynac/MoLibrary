using System.Collections.Concurrent;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Owns process-local indexing activity state used to distinguish live work from stale persisted states.
/// </summary>
internal sealed class RAGIndexingActivity
{
    private readonly ConcurrentDictionary<string, byte> _activeDocuments =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns whether a document is actively indexing in this process.</summary>
    public bool IsActive(string knowledgeBaseId, string documentPath)
        => _activeDocuments.ContainsKey(BuildKey(knowledgeBaseId, documentPath));

    /// <summary>Marks a document as active and returns a lease that clears the marker.</summary>
    public IDisposable Begin(string knowledgeBaseId, string documentPath)
    {
        var key = BuildKey(knowledgeBaseId, documentPath);
        if (!_activeDocuments.TryAdd(key, 0))
        {
            throw new InvalidOperationException(
                $"Document '{documentPath}' in knowledge base '{knowledgeBaseId}' is already indexing.");
        }

        return new ActivityLease(_activeDocuments, key);
    }

    private static string BuildKey(string knowledgeBaseId, string documentPath)
        => $"{knowledgeBaseId}::{documentPath}";

    private sealed class ActivityLease(ConcurrentDictionary<string, byte> activeDocuments, string key) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                activeDocuments.TryRemove(key, out _);
            }
        }
    }
}
