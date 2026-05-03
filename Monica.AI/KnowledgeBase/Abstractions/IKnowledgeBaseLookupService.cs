using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Abstractions;

/// <summary>
/// Lookup-only operations over knowledge bases and their source document inventory.
/// </summary>
public interface IKnowledgeBaseLookupService
{
    /// <summary>
    /// Lists all knowledge bases.
    /// </summary>
    Task<IReadOnlyList<KnowledgeBaseSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets one knowledge-base summary by id.
    /// </summary>
    Task<KnowledgeBaseSummary?> GetSummaryAsync(string kbId, CancellationToken ct = default);

    /// <summary>
    /// Lists documents recorded in a knowledge base.
    /// </summary>
    Task<IReadOnlyList<KnowledgeDocumentSummary>> BrowseDocumentsAsync(
        string kbId,
        string? directoryPath = null,
        int maxResults = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Searches documents in a knowledge base using fuzzy metadata/content matching or regular expressions.
    /// </summary>
    Task<IReadOnlyList<KnowledgeDocumentSearchResult>> SearchDocumentsAsync(
        string kbId,
        string query,
        KnowledgeDocumentSearchMode mode = KnowledgeDocumentSearchMode.Fuzzy,
        string? directoryPath = null,
        bool includeContent = true,
        int maxResults = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a directory tree built from knowledge-base document paths.
    /// </summary>
    Task<KnowledgeDocumentTreeNode?> GetDocumentTreeAsync(
        string kbId,
        string? directoryPath = null,
        int maxDepth = 3,
        CancellationToken ct = default);

    /// <summary>
    /// Reads one source document content segment when source storage is available.
    /// </summary>
    Task<KnowledgeDocumentContent?> GetDocumentContentAsync(
        string kbId,
        string documentId,
        int maxCharacters = 8000,
        int startCharacterIndex = 0,
        CancellationToken ct = default);
}
