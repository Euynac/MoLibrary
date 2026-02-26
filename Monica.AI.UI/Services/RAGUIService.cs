using Microsoft.Extensions.Logging;
using Monica.AI.Models;
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

    #region Embedding Model Management

    /// <summary>
    /// Get all available embedding models.
    /// </summary>
    public Task<Res<IReadOnlyList<EmbeddingModelInfo>>> GetEmbeddingModelsAsync()
    {
        try
        {
            // TODO: Implement actual embedding model discovery from configuration or service
            // For now, return hardcoded list matching prototype
            var models = new List<EmbeddingModelInfo>
            {
                new()
                {
                    ModelName = "text-embedding-3-large",
                    Description = "OpenAI Text Embedding 3 Large",
                    Dimensions = 3072,
                    MaxInputTokens = 8191,
                    CostPerMillionTokens = 0.13m
                },
                new()
                {
                    ModelName = "text-embedding-3-small",
                    Description = "OpenAI Text Embedding 3 Small",
                    Dimensions = 1536,
                    MaxInputTokens = 8191,
                    CostPerMillionTokens = 0.02m
                },
                new()
                {
                    ModelName = "text-embedding-ada-002",
                    Description = "OpenAI Text Embedding Ada 002 (Legacy)",
                    Dimensions = 1536,
                    MaxInputTokens = 8191,
                    CostPerMillionTokens = 0.10m
                },
                new()
                {
                    ModelName = "bge-large-zh-v1.5",
                    Description = "BAAI BGE Large Chinese v1.5",
                    Dimensions = 1024
                }
            };

            return Task.FromResult(Res.Ok<IReadOnlyList<EmbeddingModelInfo>>(models));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get embedding models");
            return Task.FromResult<Res<IReadOnlyList<EmbeddingModelInfo>>>(
                Res.Fail($"Failed to load embedding models: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get the embedding model for a knowledge base.
    /// </summary>
    public async Task<Res<EmbeddingModelInfo?>> GetKnowledgeBaseEmbeddingModelAsync(string kbId)
    {
        try
        {
            var kbs = await ragService.GetKnowledgeBasesAsync();
            var kb = kbs.FirstOrDefault(k => k.Id == kbId);

            if (kb is null)
                return Res.Fail("Knowledge base not found.");

            if (string.IsNullOrEmpty(kb.EmbeddingModelId))
                return Res.Ok<EmbeddingModelInfo?>(null);

            var modelsResult = await GetEmbeddingModelsAsync();
            if (modelsResult.IsFailed(out var error, out var models))
                return Res.Fail(error.Message!);

            var model = models.FirstOrDefault(m => m.ModelName == kb.EmbeddingModelId);
            return Res.Ok(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get embedding model for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to get embedding model: {ex.Message}");
        }
    }

    /// <summary>
    /// Set the embedding model for a knowledge base.
    /// Can only be set if the KB has no documents indexed yet.
    /// </summary>
    public async Task<Res> SetKnowledgeBaseEmbeddingModelAsync(string kbId, string modelId)
    {
        try
        {
            // TODO: Implement actual embedding model setting in RAGService
            // For now, this is a placeholder that validates the operation

            var kbs = await ragService.GetKnowledgeBasesAsync();
            var kb = kbs.FirstOrDefault(k => k.Id == kbId);

            if (kb is null)
                return Res.Fail("Knowledge base not found.");

            if (kb.DocumentCount > 0 && !string.IsNullOrEmpty(kb.EmbeddingModelId))
                return Res.Fail("Cannot change embedding model after documents have been indexed.");

            // Validate model exists
            var modelsResult = await GetEmbeddingModelsAsync();
            if (modelsResult.IsFailed(out var error, out var models))
                return Res.Fail(error.Message!);

            if (!models.Any(m => m.ModelName == modelId))
                return Res.Fail($"Embedding model '{modelId}' not found.");

            // TODO: Call RAGService to persist the embedding model ID
            logger.LogInformation("Set embedding model '{ModelId}' for KB '{KbId}'", modelId, kbId);

            return Res.Ok($"Embedding model set to '{modelId}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set embedding model for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to set embedding model: {ex.Message}");
        }
    }

    #endregion

    #region Document Queue Management

    /// <summary>
    /// Get all documents in a knowledge base with their status.
    /// </summary>
    public async Task<Res<IReadOnlyList<DocumentQueueItem>>> GetDocumentQueueAsync(string kbId)
    {
        try
        {
            // TODO: Implement actual document queue retrieval from RAGService
            // For now, return empty list
            logger.LogInformation("Getting document queue for KB '{KbId}'", kbId);

            var queue = new List<DocumentQueueItem>();
            return Res.Ok<IReadOnlyList<DocumentQueueItem>>(queue);
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
            // TODO: Implement actual document queue addition
            var count = documentIds.Count();
            logger.LogInformation("Adding {Count} documents to queue for KB '{KbId}'", count, kbId);

            return Res.Ok($"Added {count} documents to queue.");
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
            // TODO: Implement actual document removal from RAGService
            logger.LogInformation("Removing document '{DocumentId}' from KB '{KbId}'", documentId, kbId);

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
            // TODO: Implement actual document reindexing
            logger.LogInformation("Reindexing document '{DocumentId}' in KB '{KbId}'", documentId, kbId);

            progress?.Report(new IndexingProgress(1, 1, "Reindexing..."));

            return Res.Ok("Document reindexed successfully.");
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
            // TODO: Implement actual batch indexing with parallel processing
            logger.LogInformation(
                "Starting batch indexing for KB '{KbId}' with max concurrency {MaxConcurrency}",
                kbId, maxConcurrency);

            progress?.Report(new IndexingProgress(0, 0, "Starting batch indexing..."));

            return Res.Ok("Batch indexing started.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start batch indexing for KB '{KbId}'", kbId);
            return Res.Fail($"Failed to start batch indexing: {ex.Message}");
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
