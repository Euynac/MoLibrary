using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;

namespace Monica.AI.KnowledgeBase.Services;

/// <summary>
/// Owns knowledge-base document import, inventory, source-content, and removal operations.
/// </summary>
internal sealed class KnowledgeDocumentService(
    IKnowledgeBaseStore store,
    IKnowledgeDocumentSourceStore sourceStore,
    IMarkdownDocumentCatalog markdownCatalog,
    IEnumerable<IKnowledgeBaseLifecycleHandler> lifecycleHandlers)
{
    /// <summary>
    /// Returns the document inventory for a knowledge base.
    /// </summary>
    public async Task<IReadOnlyList<DocumentQueueItem>> GetInventoryAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        var knowledgeBase = await GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        return await store.GetDocumentInventoryAsync(knowledgeBase.Id, ct);
    }

    /// <summary>
    /// Imports markdown documents as pending inventory records.
    /// </summary>
    public async Task<KnowledgeBaseDocumentImportResult> ImportMarkdownAsync(
        string knowledgeBaseId,
        string sourceGroupKey,
        IEnumerable<string> documentIds,
        CancellationToken ct = default)
    {
        var knowledgeBase = await GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var normalizedGroupKey = NormalizeRequiredText(sourceGroupKey, nameof(sourceGroupKey));
        var selectedIds = documentIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedIds.Count == 0)
        {
            throw new ArgumentException("At least one document must be selected.", nameof(documentIds));
        }

        var states = (await markdownCatalog.GetDocumentsAsync(normalizedGroupKey))
            .Where(document => selectedIds.Contains(document.RelativePath))
            .Select(document => CreatePendingDocumentState(
                knowledgeBase.Id,
                document.RelativePath,
                document.Title,
                KnowledgeDocumentSourceKinds.Markdown,
                normalizedGroupKey))
            .ToList();

        var result = await store.AddPendingDocumentsAsync(knowledgeBase.Id, states, ct);
        await RefreshStatisticsAsync(knowledgeBase, ct);
        return result;
    }

    /// <summary>
    /// Saves one uploaded document and adds its pending inventory record.
    /// </summary>
    public async Task UploadAsync(
        string knowledgeBaseId,
        string fileName,
        string content,
        CancellationToken ct = default)
    {
        var knowledgeBase = await GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        var normalizedFileName = NormalizeRequiredText(fileName, nameof(fileName));
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Uploaded document content cannot be empty.", nameof(content));
        }

        var result = await store.AddPendingDocumentsAsync(
            knowledgeBase.Id,
            [CreatePendingDocumentState(
                knowledgeBase.Id,
                normalizedFileName,
                normalizedFileName,
                KnowledgeDocumentSourceKinds.Uploaded,
                sourceGroupKey: null)],
            ct);

        if (result.AddedCount == 0)
        {
            throw new InvalidOperationException(
                $"Document '{normalizedFileName}' already exists in knowledge base '{knowledgeBase.Id}'.");
        }

        await sourceStore.SaveContentAsync(knowledgeBase.Id, normalizedFileName, content, ct);
        await RefreshStatisticsAsync(knowledgeBase, ct);
    }

    /// <summary>
    /// Returns source content and metadata for one document.
    /// </summary>
    public async Task<KnowledgeBaseDocumentPreview?> GetPreviewAsync(
        string knowledgeBaseId,
        string documentId,
        CancellationToken ct = default)
    {
        var inventory = await GetInventoryAsync(knowledgeBaseId, ct);
        var document = inventory.FirstOrDefault(item =>
            string.Equals(item.Id, documentId, StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            return null;
        }

        var content = await GetContentAsync(knowledgeBaseId, document, ct);
        return content is null
            ? null
            : new KnowledgeBaseDocumentPreview
            {
                KnowledgeBaseId = knowledgeBaseId,
                DocumentId = document.Id,
                DocumentName = document.Name,
                Content = content,
                SourceKind = document.SourceKind ?? KnowledgeDocumentSourceKinds.Unknown,
                SourceGroupKey = document.SourceGroupKey
            };
    }

    /// <summary>
    /// Resolves persisted or markdown-backed source content for an inventory item.
    /// </summary>
    public async Task<string?> GetContentAsync(
        string knowledgeBaseId,
        DocumentQueueItem document,
        CancellationToken ct = default)
    {
        var storedContent = await sourceStore.GetContentAsync(knowledgeBaseId, document.Id, ct);
        if (storedContent is not null)
        {
            return storedContent;
        }

        if (!string.Equals(document.SourceKind, KnowledgeDocumentSourceKinds.Markdown, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(document.SourceGroupKey))
        {
            var groupDocuments = await markdownCatalog.GetDocumentsAsync(document.SourceGroupKey);
            var matchedDocument = groupDocuments.FirstOrDefault(item =>
                string.Equals(item.RelativePath, document.Id, StringComparison.OrdinalIgnoreCase));
            if (matchedDocument is not null)
            {
                return await markdownCatalog.GetDocumentContentAsync(matchedDocument);
            }
        }

        var markdownDocument = await markdownCatalog.GetDocumentByPathAsync(document.Id);
        return await markdownCatalog.GetDocumentContentAsync(markdownDocument);
    }

    /// <summary>
    /// Removes one document after optional feature handlers release owned resources.
    /// </summary>
    public async Task RemoveAsync(
        string knowledgeBaseId,
        string documentId,
        CancellationToken ct = default)
    {
        var knowledgeBase = await GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        foreach (var handler in lifecycleHandlers)
        {
            await handler.OnDocumentRemovingAsync(knowledgeBase.Id, documentId, ct);
        }

        await store.DeleteDocumentAsync(knowledgeBase.Id, documentId, ct);
        await sourceStore.DeleteContentAsync(knowledgeBase.Id, documentId, ct);
        await RefreshStatisticsAsync(knowledgeBase, ct);
    }

    /// <summary>
    /// Clears all documents after optional feature handlers release owned resources.
    /// </summary>
    public async Task<int> ClearAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var knowledgeBase = await GetKnowledgeBaseRequiredAsync(knowledgeBaseId, ct);
        foreach (var handler in lifecycleHandlers)
        {
            await handler.OnDocumentsClearingAsync(knowledgeBase.Id, ct);
        }

        var removedCount = await store.DeleteDocumentsAsync(knowledgeBase.Id, ct);
        await sourceStore.DeleteKnowledgeBaseAsync(knowledgeBase.Id, ct);
        knowledgeBase.DocumentCount = 0;
        knowledgeBase.ChunkCount = 0;
        await store.UpsertKnowledgeBaseAsync(knowledgeBase, ct);
        return removedCount;
    }

    private async Task<Models.KnowledgeBase> GetKnowledgeBaseRequiredAsync(
        string knowledgeBaseId,
        CancellationToken ct)
    {
        var normalizedId = KnowledgeBaseIdPolicy.Normalize(knowledgeBaseId);
        return await store.GetKnowledgeBaseAsync(normalizedId, ct)
               ?? throw new KeyNotFoundException($"Knowledge base '{normalizedId}' was not found.");
    }

    private async Task RefreshStatisticsAsync(Models.KnowledgeBase knowledgeBase, CancellationToken ct)
    {
        var documents = await store.GetDocumentInventoryAsync(knowledgeBase.Id, ct);
        knowledgeBase.DocumentCount = documents.Count;
        knowledgeBase.ChunkCount = documents.Sum(static document => Math.Max(0, document.ChunkCount));
        await store.UpsertKnowledgeBaseAsync(knowledgeBase, ct);
    }

    private static DocumentIndexState CreatePendingDocumentState(
        string knowledgeBaseId,
        string documentPath,
        string? documentName,
        string sourceKind,
        string? sourceGroupKey)
    {
        var resolvedName = string.IsNullOrWhiteSpace(documentName)
            ? Path.GetFileName(documentPath)
            : documentName.Trim();

        return new DocumentIndexState
        {
            KnowledgeBaseId = knowledgeBaseId,
            DocumentPath = documentPath,
            DocumentName = string.IsNullOrWhiteSpace(resolvedName) ? documentPath : resolvedName,
            Status = DocumentStatus.Pending,
            SourceKind = sourceKind,
            SourceGroupKey = sourceGroupKey,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
