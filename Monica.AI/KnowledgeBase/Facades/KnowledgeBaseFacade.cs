using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.AI.KnowledgeBase.Models;
using Monica.AI.KnowledgeBase.Services;
using Monica.AI.RAG.Services;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.KnowledgeBase.Facades;

/// <summary>
/// Host-facing facade for knowledge-base CRUD and document-inventory operations.
/// </summary>
public sealed class KnowledgeBaseFacade(
    IServiceProvider serviceProvider,
    KnowledgeBaseService knowledgeBaseService,
    ILogger<KnowledgeBaseFacade> logger)
{
    /// <summary>
    /// Returns all knowledge bases.
    /// </summary>
    public async Task<Res<IReadOnlyList<Models.KnowledgeBase>>> GetAllAsync()
    {
        try
        {
            return Res.Ok(await knowledgeBaseService.GetAllAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get knowledge bases.");
            return Res.Fail($"Failed to load knowledge bases: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Returns one knowledge base by id.
    /// </summary>
    public async Task<Res<Models.KnowledgeBase>> GetByIdAsync(string id)
    {
        try
        {
            var knowledgeBase = await knowledgeBaseService.GetByIdAsync(id);
            return knowledgeBase is null
                ? Res.Fail($"Knowledge base '{id}' was not found.")
                : Res.Ok(knowledgeBase);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get knowledge base '{KnowledgeBaseId}'.", id);
            return Res.Fail($"Failed to load knowledge base: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Creates a new knowledge base.
    /// </summary>
    public async Task<Res<Models.KnowledgeBase>> CreateAsync(
        string id,
        string name,
        string? description = null)
    {
        try
        {
            return await knowledgeBaseService.CreateAsync(id, name, description);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create knowledge base '{KnowledgeBaseId}' ('{Name}').", id, name);
            return Res.Fail($"Failed to create knowledge base: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Updates an existing knowledge base.
    /// </summary>
    public async Task<Res<Models.KnowledgeBase>> UpdateAsync(
        string id,
        string name,
        string? description = null)
    {
        try
        {
            return await knowledgeBaseService.UpdateAsync(id, name, description);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update knowledge base '{KnowledgeBaseId}'.", id);
            return Res.Fail($"Failed to update knowledge base: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Deletes a knowledge base. RAG vector/source cleanup is performed when the RAG service is registered.
    /// </summary>
    public async Task<Res> DeleteAsync(string id)
    {
        try
        {
            if (serviceProvider.GetService<RAGService>() is { } ragService)
            {
                await ragService.DeleteKnowledgeBaseAsync(id);
            }
            else
            {
                await knowledgeBaseService.DeleteAsync(id);
            }

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete knowledge base '{KnowledgeBaseId}'.", id);
            return Res.Fail($"Failed to delete knowledge base: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Returns the document inventory recorded for a knowledge base.
    /// </summary>
    public async Task<Res<IReadOnlyList<DocumentQueueItem>>> GetDocumentInventoryAsync(string kbId)
    {
        try
        {
            return Res.Ok(await knowledgeBaseService.GetDocumentInventoryAsync(kbId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get document inventory for KB '{KnowledgeBaseId}'.", kbId);
            return Res.Fail($"Failed to load document inventory: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Removes one document from a knowledge base. RAG vector/source cleanup is performed when the RAG service is registered.
    /// </summary>
    public async Task<Res> RemoveDocumentAsync(string kbId, string documentId)
    {
        try
        {
            if (serviceProvider.GetService<RAGService>() is { } ragService)
            {
                await ragService.RemoveDocumentAsync(kbId, documentId);
            }
            else
            {
                await knowledgeBaseService.RemoveDocumentAsync(kbId, documentId);
            }

            return Res.Ok("Document removed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to remove document '{DocumentId}' from KB '{KnowledgeBaseId}'.",
                documentId,
                kbId);
            return Res.Fail($"Failed to remove document: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Clears all documents from a knowledge base. RAG vector/source cleanup is performed when the RAG service is registered.
    /// </summary>
    public async Task<Res<int>> ClearDocumentsAsync(string kbId)
    {
        try
        {
            var removedCount = serviceProvider.GetService<RAGService>() is { } ragService
                ? await ragService.ClearKnowledgeBaseDocumentsAsync(kbId)
                : await knowledgeBaseService.ClearDocumentsAsync(kbId);

            return Res.Ok(removedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear documents for KB '{KnowledgeBaseId}'.", kbId);
            return Res.Fail($"Failed to clear documents: {ex.GetMessageRecursively()}");
        }
    }
}
