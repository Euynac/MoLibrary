using System.Collections.Concurrent;
using Monica.AI.RAG.Abstractions;
using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Stores;

/// <summary>
/// In-memory implementation of document queue store.
/// </summary>
public class InMemoryDocumentQueueStore : IDocumentQueueStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DocumentQueueItem>> _queues = new();

    public Task<IReadOnlyList<DocumentQueueItem>> GetQueueAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        if (_queues.TryGetValue(knowledgeBaseId, out var queue))
        {
            return Task.FromResult<IReadOnlyList<DocumentQueueItem>>(queue.Values.ToList());
        }
        return Task.FromResult<IReadOnlyList<DocumentQueueItem>>(Array.Empty<DocumentQueueItem>());
    }

    public Task AddAsync(DocumentQueueItem item, CancellationToken ct = default)
    {
        var queue = _queues.GetOrAdd(item.KnowledgeBaseId, _ => new ConcurrentDictionary<string, DocumentQueueItem>());
        queue[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DocumentQueueItem item, CancellationToken ct = default)
    {
        if (_queues.TryGetValue(item.KnowledgeBaseId, out var queue))
        {
            queue[item.Id] = item;
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string knowledgeBaseId, string documentId, CancellationToken ct = default)
    {
        if (_queues.TryGetValue(knowledgeBaseId, out var queue))
        {
            queue.TryRemove(documentId, out _);
        }
        return Task.CompletedTask;
    }

    public Task<DocumentQueueItem?> GetByIdAsync(string knowledgeBaseId, string documentId, CancellationToken ct = default)
    {
        if (_queues.TryGetValue(knowledgeBaseId, out var queue) &&
            queue.TryGetValue(documentId, out var item))
        {
            return Task.FromResult<DocumentQueueItem?>(item);
        }
        return Task.FromResult<DocumentQueueItem?>(null);
    }
}
