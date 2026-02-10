using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Abstract persistence for knowledge base metadata.
/// Developers provide their own implementation (EF Core, file, etc.).
/// </summary>
public interface IKnowledgeBaseStore
{
    Task<IReadOnlyList<KnowledgeBase>> GetAllAsync(CancellationToken ct = default);
    Task<KnowledgeBase?> GetByIdAsync(string id, CancellationToken ct = default);
    Task SaveAsync(KnowledgeBase knowledgeBase, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
