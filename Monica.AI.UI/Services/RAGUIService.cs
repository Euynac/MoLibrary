using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Models;
using Monica.AI.RAG.Services;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Tool.MoResponse;

namespace Monica.AI.UI.Services;

/// <summary>
/// UI service for RAG operations. Uses Res&lt;T&gt; for Blazor consumption.
/// Wraps the infrastructure RAGService, catching exceptions and returning Res.
/// </summary>
public class RAGUIService(
    RAGService ragService,
    IMoMarkdownService markdownService,
    ILogger<RAGUIService> logger)
{
    public async Task<Res<IReadOnlyList<KnowledgeBase>>> GetKnowledgeBasesAsync()
    {
        try
        {
            var result = await ragService.GetKnowledgeBasesAsync();
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get knowledge bases");
            return Res.Fail($"Failed to load knowledge bases: {ex.Message}");
        }
    }

    public async Task<Res<KnowledgeBase>> CreateKnowledgeBaseAsync(
        string name, string? description = null)
    {
        try
        {
            var kb = await ragService.CreateKnowledgeBaseAsync(name, description);
            return kb;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create knowledge base '{Name}'", name);
            return Res.Fail($"Failed to create knowledge base: {ex.Message}");
        }
    }

    public async Task<Res> DeleteKnowledgeBaseAsync(string id)
    {
        try
        {
            await ragService.DeleteKnowledgeBaseAsync(id);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete knowledge base '{Id}'", id);
            return Res.Fail($"Failed to delete knowledge base: {ex.Message}");
        }
    }

    public async Task<Res<IReadOnlyList<TextSearchResult>>> SearchAsync(
        string query, IEnumerable<string> kbIds, int topK = 5)
    {
        try
        {
            var results = await ragService.SearchAsync(query, kbIds, topK);
            return Res.Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query '{Query}'", query);
            return Res.Fail($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Index all documents from a markdown group into a knowledge base.
    /// </summary>
    public async Task<Res> IndexMarkdownGroupAsync(
        string kbId, string groupKey,
        IProgress<IndexingProgress>? progress = null)
    {
        try
        {
            var documents = await markdownService.GetDocumentsAsync(groupKey);
            if (documents.Count == 0)
                return Res.Fail("No documents found in the selected group.");

            var indexed = 0;
            foreach (var doc in documents)
            {
                var content = await markdownService.GetDocumentContentAsync(doc);
                await ragService.IndexDocumentAsync(
                    kbId, doc.RelativePath, doc.Title, content, progress);
                indexed++;
                progress?.Report(new IndexingProgress(indexed, documents.Count, doc.Title));
            }

            return Res.Ok($"Indexed {indexed} documents.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index markdown group '{GroupKey}' into KB '{KbId}'",
                groupKey, kbId);
            return Res.Fail($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Index a single uploaded document into a knowledge base.
    /// </summary>
    public async Task<Res> UploadAndIndexDocumentAsync(
        string kbId, string fileName, string content)
    {
        try
        {
            await ragService.IndexDocumentAsync(kbId, fileName, fileName, content);
            return Res.Ok($"Indexed '{fileName}' successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index uploaded document '{FileName}'", fileName);
            return Res.Fail($"Upload indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all registered markdown document groups (for the indexing panel).
    /// </summary>
    public async Task<Res<List<MarkdownDocumentGroup>>> GetMarkdownGroupsAsync()
    {
        try
        {
            var groups = await markdownService.GetAllDocumentGroupsAsync();
            return groups;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get markdown groups");
            return Res.Fail($"Failed to load markdown groups: {ex.Message}");
        }
    }
}
