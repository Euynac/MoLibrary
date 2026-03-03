using Microsoft.Extensions.Logging;
using Monica.AI.RAG.Abstractions;
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
    IDocumentQueueStore documentQueueStore,
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

    #region Document Queue Management

    /// <summary>
    /// Get all documents in a knowledge base with their status.
    /// </summary>
    public async Task<Res<IReadOnlyList<DocumentQueueItem>>> GetDocumentQueueAsync(string kbId)
    {
        try
        {
            var queue = await documentQueueStore.GetQueueAsync(kbId);
            return Res.Ok(queue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get document queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to load document queue: {ex.Message}");
        }
    }

    /// <summary>
    /// Add documents to the indexing queue.
    /// </summary>
    public async Task<Res> AddDocumentsToQueueAsync(string kbId, IEnumerable<string> documentIds)
    {
        try
        {
            var docIdList = documentIds.ToList();
            logger.LogInformation("Adding {Count} documents to queue for KB '{KbId}'", docIdList.Count, kbId);

            // Get the markdown group to fetch document details
            foreach (var docId in docIdList)
            {
                // Check if document already exists in queue
                var existing = await documentQueueStore.GetByIdAsync(kbId, docId);
                if (existing != null)
                {
                    logger.LogWarning("Document '{DocId}' already exists in queue for KB '{KbId}'", docId, kbId);
                    continue;
                }

                // Create queue item
                var queueItem = new DocumentQueueItem
                {
                    Id = docId,
                    Name = Path.GetFileName(docId),
                    KnowledgeBaseId = kbId,
                    Status = DocumentStatus.Pending,
                    ChunkCount = 0,
                    Progress = 0
                };

                await documentQueueStore.AddAsync(queueItem);
            }

            return Res.Ok($"Added {docIdList.Count} documents to queue.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add documents to queue for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to add documents: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove a document from the queue or delete an indexed document.
    /// </summary>
    public async Task<Res> RemoveDocumentAsync(string kbId, string documentId)
    {
        try
        {
            logger.LogInformation("Removing document '{DocumentId}' from KB '{KbId}'", documentId, kbId);

            // Remove from queue
            await documentQueueStore.RemoveAsync(kbId, documentId);

            // TODO: Also remove from vector store if indexed
            // This would require RAGService to have a RemoveDocument method

            return Res.Ok("Document removed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove document '{DocumentId}' from KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to remove document: {ex.Message}");
        }
    }

    /// <summary>
    /// Re-index an existing document.
    /// </summary>
    public async Task<Res> ReindexDocumentAsync(
        string kbId, string documentId, IProgress<IndexingProgress>? progress = null)
    {
        try
        {
            logger.LogInformation("Reindexing document '{DocumentId}' in KB '{KbId}'", documentId, kbId);

            var queueItem = await documentQueueStore.GetByIdAsync(kbId, documentId);
            if (queueItem == null)
            {
                return Res.Fail("Document not found in queue.");
            }

            // Update status to pending
            queueItem.Status = DocumentStatus.Pending;
            queueItem.Progress = 0;
            queueItem.ErrorMessage = null;
            await documentQueueStore.UpdateAsync(queueItem);

            return Res.Ok("Document queued for reindexing.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reindex document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to reindex document: {ex.Message}");
        }
    }

    /// <summary>
    /// Start batch indexing with parallel control.
    /// </summary>
    public async Task<Res> StartBatchIndexingAsync(
        string kbId,
        int maxConcurrency = 5,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Starting batch indexing for KB '{KbId}' with max concurrency {MaxConcurrency}",
                kbId, maxConcurrency);

            // Get pending documents from queue
            var queue = await documentQueueStore.GetQueueAsync(kbId, cancellationToken);
            var pendingDocs = queue.Where(d => d.Status == DocumentStatus.Pending).ToList();

            if (pendingDocs.Count == 0)
            {
                return Res.Fail("No pending documents to index.");
            }

            // Process documents with limited concurrency
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = pendingDocs.Select(async doc =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await IndexQueuedDocumentAsync(kbId, doc, progress, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return Res.Ok($"Batch indexing completed for {pendingDocs.Count} documents.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start batch indexing for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to start batch indexing: {ex.Message}");
        }
    }

    private async Task IndexQueuedDocumentAsync(
        string kbId,
        DocumentQueueItem queueItem,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Update status to indexing
            queueItem.Status = DocumentStatus.Indexing;
            queueItem.Progress = 0;
            await documentQueueStore.UpdateAsync(queueItem, cancellationToken);

            // Get document content from markdown service
            // The document ID is the relative path
            var groups = await markdownService.GetAllDocumentGroupsAsync();
            MarkdownDocument? document = null;

            foreach (var group in groups)
            {
                var docs = await markdownService.GetDocumentsAsync(group.Key);
                document = docs.FirstOrDefault(d => d.RelativePath == queueItem.Id);
                if (document != null) break;
            }

            if (document == null)
            {
                throw new FileNotFoundException($"Document '{queueItem.Id}' not found in any markdown group.");
            }

            var content = await markdownService.GetDocumentContentAsync(document);

            // Index the document
            var indexProgress = new Progress<IndexingProgress>(p =>
            {
                if (p.TotalChunks > 0)
                {
                    queueItem.Progress = (int)((p.ProcessedChunks / (double)p.TotalChunks) * 100);
                    queueItem.ChunkCount = p.TotalChunks;
                }

                documentQueueStore.UpdateAsync(queueItem, cancellationToken).Wait();
                progress?.Report(p);
            });

            await ragService.IndexDocumentAsync(
                kbId,
                document.RelativePath,
                document.Title,
                content,
                indexProgress,
                cancellationToken);

            // Update status to done
            queueItem.Status = DocumentStatus.Done;
            queueItem.Progress = 100;
            queueItem.IndexedAt = DateTimeOffset.UtcNow;
            queueItem.ErrorMessage = null;
            await documentQueueStore.UpdateAsync(queueItem, cancellationToken);

            logger.LogInformation("Successfully indexed document '{DocId}' in KB '{KbId}'", queueItem.Id, kbId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index document '{DocId}' in KB '{KbId}'", queueItem.Id, kbId);

            queueItem.Status = DocumentStatus.Error;
            queueItem.ErrorMessage = ex.Message;
            await documentQueueStore.UpdateAsync(queueItem, cancellationToken);
        }
    }

    /// <summary>
    /// Get available documents from a markdown group that can be added to the queue.
    /// </summary>
    public async Task<Res<IReadOnlyList<MarkdownDocument>>> GetAvailableDocumentsAsync(string groupKey)
    {
        try
        {
            var documents = await markdownService.GetDocumentsAsync(groupKey);
            return Res.Ok<IReadOnlyList<MarkdownDocument>>(documents);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get available documents from group '{GroupKey}'", groupKey);
            return Res.Fail($"Failed to load documents: {ex.Message}");
        }
    }

    #endregion

    #region Chunk Viewer

    /// <summary>
    /// Get document original text and chunk highlights for the chunk viewer.
    /// </summary>
    public async Task<Res<(string OriginalText, IReadOnlyList<ChunkHighlight> Chunks)>> GetDocumentChunksAsync(
        string kbId,
        string documentId)
    {
        try
        {
            // TODO: Implement actual document chunk retrieval from RAGService
            logger.LogInformation("Getting chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);

            // Mock implementation with sample data
            var originalText = @"# Getting Started

Welcome to our product! This guide will help you get started quickly with the basic setup and configuration.

## Installation

To install the product, run: npm install @product/core. Make sure you have Node.js 18+ installed.

You can verify the installation by running: npm list @product/core

## Configuration

Create a config.json file in your project root with the following structure: { ""apiKey"": ""your-key"", ""endpoint"": ""https://api.example.com"" }

The configuration file supports multiple environments. You can create separate config files for development, staging, and production.

## First Steps

After installation and configuration, you can start using the product by importing it in your code:

import { Product } from '@product/core';

const product = new Product(config);

## Next Steps

Check out the API reference for detailed information about available methods and options. Visit our documentation portal for tutorials and examples.";

            var chunks = new List<ChunkHighlight>
            {
                new()
                {
                    Index = 0,
                    Start = 20,
                    End = 130,
                    Section = "Introduction",
                    IsMatched = false
                },
                new()
                {
                    Index = 1,
                    Start = 150,
                    End = 280,
                    Section = "Installation",
                    IsMatched = false
                },
                new()
                {
                    Index = 2,
                    Start = 380,
                    End = 550,
                    Section = "Configuration",
                    IsMatched = false
                }
            };

            return Res.Ok((originalText, (IReadOnlyList<ChunkHighlight>)chunks));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get chunks for document '{DocumentId}' in KB '{KbId}'", documentId, kbId);
            return Res.Fail($"Failed to load document chunks: {ex.Message}");
        }
    }

    #endregion
}
