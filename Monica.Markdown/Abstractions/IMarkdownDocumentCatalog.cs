using Monica.Markdown.Models;
using Monica.Tool.Algorithms.Trees;

namespace Monica.Markdown.Abstractions;

/// <summary>
/// Service for managing and querying markdown document groups.
/// Provides scanning, tree navigation, content retrieval, and metadata search.
/// </summary>
public interface IMarkdownDocumentCatalog
{
    /// <summary>
    /// Gets all registered document groups with their validity and document counts.
    /// </summary>
    Task<List<MarkdownDocumentGroup>> GetAllDocumentGroupsAsync();

    /// <summary>
    /// Gets a single document group by its key.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the group key is not found.</exception>
    Task<MarkdownDocumentGroup> GetDocumentGroupAsync(string groupKey);

    /// <summary>
    /// Gets a flat list of all documents in a group.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the group key is not found.</exception>
    Task<List<MarkdownDocument>> GetDocumentsAsync(string groupKey);

    /// <summary>
    /// Gets the hierarchical tree structure for a document group.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the group key is not found.</exception>
    Task<TreeNode<MarkdownDocumentNodeData>> GetDocumentTreeAsync(string groupKey);

    /// <summary>
    /// Looks up a document by its absolute or relative file path.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no document matches the path.</exception>
    Task<MarkdownDocument> GetDocumentByPathAsync(string filePath);

    /// <summary>
    /// Reads the raw markdown content of a document.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist on disk.</exception>
    Task<string> GetDocumentContentAsync(MarkdownDocument doc);

    /// <summary>
    /// Reads the raw markdown content of a document by its file path.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist on disk.</exception>
    Task<string> GetDocumentContentByPathAsync(string filePath);

    /// <summary>
    /// Searches documents by a single metadata field value.
    /// </summary>
    /// <param name="key">Metadata key to search.</param>
    /// <param name="value">Value to match (case-insensitive).</param>
    /// <param name="groupKeys">Optional group keys to limit search scope. Null searches all groups.</param>
    Task<List<MarkdownDocument>> SearchByMetadataAsync(
        string key, string value, IEnumerable<string>? groupKeys = null);

    /// <summary>
    /// Searches documents by multiple metadata field values (AND logic).
    /// </summary>
    /// <param name="criteria">Key-value pairs to match (all must match).</param>
    /// <param name="groupKeys">Optional group keys to limit search scope. Null searches all groups.</param>
    Task<List<MarkdownDocument>> SearchByMetadataAsync(
        Dictionary<string, string> criteria, IEnumerable<string>? groupKeys = null);

    /// <summary>
    /// Rescans all registered document groups.
    /// </summary>
    Task RefreshAllAsync();

    /// <summary>
    /// Rescans a specific document group.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the group key is not registered.</exception>
    Task RefreshAsync(string groupKey);
}