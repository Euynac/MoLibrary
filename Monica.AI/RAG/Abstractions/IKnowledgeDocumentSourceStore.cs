namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Store for raw source document content used to rebuild chunk views in real time.
/// </summary>
public interface IKnowledgeDocumentSourceStore
{
    Task SaveContentAsync(
        string knowledgeBaseId,
        string documentPath,
        string content,
        CancellationToken ct = default);

    Task<string?> GetContentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default);

    Task DeleteContentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default);

    Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default);
}
