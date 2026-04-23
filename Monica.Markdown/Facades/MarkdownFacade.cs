using Microsoft.Extensions.Logging;
using Monica.Core.Results;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.Models;
using Monica.Tool.Algorithms.Trees;

namespace Monica.Markdown.Facades;

/// <summary>
/// Host-facing markdown entry point for UI components and other presentation adapters.
/// Converts internal exceptions into the unified <see cref="Res"/> result model.
/// </summary>
public class MarkdownFacade(
    IMarkdownDocumentCatalog markdownCatalog,
    IMarkdownDocumentSearcher markdownSearcher,
    ILogger<MarkdownFacade> logger)
{
    /// <summary>
    /// Gets all registered document groups.
    /// </summary>
    public async Task<Res<List<MarkdownDocumentGroup>>> GetAllDocumentGroupsAsync()
    {
        try
        {
            var groups = await markdownCatalog.GetAllDocumentGroupsAsync();
            return Res.Ok(groups);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load document groups.");
            return Res.Fail($"Failed to load document groups: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a single document group by key.
    /// </summary>
    public async Task<Res<MarkdownDocumentGroup>> GetDocumentGroupAsync(string groupKey)
    {
        try
        {
            var group = await markdownCatalog.GetDocumentGroupAsync(groupKey);
            return Res.Ok(group);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load document group '{GroupKey}'.", groupKey);
            return Res.Fail($"Failed to load document group: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the hierarchical tree structure for a document group.
    /// For multilingual groups, providing a culture returns the visible tree
    /// rooted at that language folder.
    /// </summary>
    public async Task<Res<TreeNode<MarkdownDocumentNodeData>>> GetDocumentTreeAsync(
        string groupKey,
        string? culture = null)
    {
        try
        {
            var tree = await markdownCatalog.GetDocumentTreeAsync(groupKey, culture);
            return Res.Ok(tree);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load document tree for group '{GroupKey}'.", groupKey);
            return Res.Fail($"Failed to load document tree: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the raw markdown content of a document.
    /// </summary>
    public async Task<Res<string>> GetDocumentContentAsync(MarkdownDocument document)
    {
        try
        {
            var content = await markdownCatalog.GetDocumentContentAsync(document);
            return Res.Ok<string>(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load markdown document content from '{Path}'.", document.FilePath);
            return Res.Fail($"Failed to load document content: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches markdown documents and returns UI-friendly result descriptors.
    /// </summary>
    public async Task<Res<IReadOnlyList<MarkdownDocumentSearchResult>>> SearchDocumentsAsync(
        MarkdownDocumentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await markdownSearcher.SearchAsync(request, cancellationToken);
            return Res.Ok<IReadOnlyList<MarkdownDocumentSearchResult>>(results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search markdown documents for query '{Query}'.", request.Query);
            return Res.Fail($"Failed to search markdown documents: {ex.Message}");
        }
    }

    /// <summary>
    /// Rescans a specific document group.
    /// </summary>
    public async Task<Res> RefreshGroupAsync(string groupKey)
    {
        try
        {
            await markdownCatalog.RefreshAsync(groupKey);
            return Res.Ok("Group refreshed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh markdown group '{GroupKey}'.", groupKey);
            return Res.Fail($"Failed to refresh group: {ex.Message}");
        }
    }

    /// <summary>
    /// Rescans all registered document groups.
    /// </summary>
    public async Task<Res> RefreshAllAsync()
    {
        try
        {
            await markdownCatalog.RefreshAllAsync();
            return Res.Ok("All groups refreshed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh all markdown groups.");
            return Res.Fail($"Failed to refresh all groups: {ex.Message}");
        }
    }
}
