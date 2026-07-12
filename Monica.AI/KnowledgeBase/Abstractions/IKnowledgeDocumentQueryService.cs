using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Abstractions;

/// <summary>
/// Canonical read-only query contract for knowledge-base metadata and source documents.
/// </summary>
public interface IKnowledgeDocumentQueryService
{
    /// <summary>Lists all knowledge bases.</summary>
    Task<IReadOnlyList<KnowledgeBaseSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>Returns one knowledge-base summary.</summary>
    Task<KnowledgeBaseSummary?> GetSummaryAsync(string knowledgeBaseId, CancellationToken ct = default);

    /// <summary>Lists documents under an optional directory.</summary>
    Task<IReadOnlyList<KnowledgeDocumentSummary>> BrowseDocumentsAsync(
        string knowledgeBaseId,
        string? directoryPath = null,
        int maxResults = 50,
        CancellationToken ct = default);

    /// <summary>Searches document metadata and optionally source content.</summary>
    Task<IReadOnlyList<KnowledgeDocumentSearchResult>> SearchDocumentsAsync(
        string knowledgeBaseId,
        string query,
        KnowledgeDocumentSearchMode mode = KnowledgeDocumentSearchMode.Fuzzy,
        string? directoryPath = null,
        bool includeContent = true,
        int maxResults = 20,
        CancellationToken ct = default);

    /// <summary>Builds a document tree rooted at an optional directory.</summary>
    Task<KnowledgeDocumentTreeNode?> GetDocumentTreeAsync(
        string knowledgeBaseId,
        string? directoryPath = null,
        int maxDepth = 3,
        CancellationToken ct = default);

    /// <summary>Returns one bounded source-content segment.</summary>
    Task<KnowledgeDocumentContent?> GetDocumentContentAsync(
        string knowledgeBaseId,
        string documentId,
        int maxCharacters = 8000,
        int startCharacterIndex = 0,
        CancellationToken ct = default);
}
