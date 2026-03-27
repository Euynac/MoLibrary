using Microsoft.Extensions.Logging;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.Models;
using Monica.Tool.Algorithm.Tree;
using Monica.Tool.Results;

namespace Monica.Markdown.UIMarkdown.Services;

/// <summary>
/// UI service wrapping markdown infrastructure services with Res&lt;T&gt; return types
/// for consumption by Blazor components.
/// </summary>
public class MarkdownUIService(
    IMoMarkdownService markdownService,
    IMarkdownDocumentSearchService documentSearchService,
    ILogger<MarkdownUIService> logger)
{
    /// <summary>
    /// Gets all registered document groups.
    /// </summary>
    public async Task<Res<List<MarkdownDocumentGroup>>> GetAllDocumentGroupsAsync()
    {
        try
        {
            var groups = await markdownService.GetAllDocumentGroupsAsync();
            return Res.Ok(groups);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load document groups");
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
            var group = await markdownService.GetDocumentGroupAsync(groupKey);
            return Res.Ok(group);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load document group: {GroupKey}", groupKey);
            return Res.Fail($"Failed to load document group: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the hierarchical tree structure for a document group.
    /// </summary>
    public async Task<Res<TreeNode<MarkdownDocumentNodeData>>> GetDocumentTreeAsync(string groupKey)
    {
        try
        {
            var tree = await markdownService.GetDocumentTreeAsync(groupKey);
            return Res.Ok(tree);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load document tree: {GroupKey}", groupKey);
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
            var content = await markdownService.GetDocumentContentAsync(document);
            return Res.Ok<string>(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load document content: {Path}", document.FilePath);
            return Res.Fail($"Failed to load document content: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches markdown documents for the supplied query and returns UI-ready results.
    /// </summary>
    public async Task<Res<IReadOnlyList<MarkdownDocumentSearchResult>>> SearchDocumentsAsync(
        MarkdownDocumentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await documentSearchService.SearchAsync(request, cancellationToken);
            return Res.Ok<IReadOnlyList<MarkdownDocumentSearchResult>>(results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search markdown documents for query '{Query}'", request.Query);
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
            await markdownService.RefreshAsync(groupKey);
            return Res.Ok("Group refreshed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh group: {GroupKey}", groupKey);
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
            await markdownService.RefreshAllAsync();
            return Res.Ok("All groups refreshed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh all groups");
            return Res.Fail($"Failed to refresh all groups: {ex.Message}");
        }
    }
}
