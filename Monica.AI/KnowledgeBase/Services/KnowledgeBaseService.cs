using Microsoft.Extensions.Logging;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Services;

/// <summary>
/// Coordinates knowledge-base metadata and inventory operations.
/// </summary>
public sealed class KnowledgeBaseService(
    IKnowledgeBaseStore store,
    IKnowledgeDocumentSourceStore sourceStore,
    ILogger<KnowledgeBaseService> logger)
{
    /// <summary>
    /// Creates a knowledge base.
    /// </summary>
    public async Task<Models.KnowledgeBase> CreateAsync(
        string id,
        string name,
        string? description = null,
        string? searchToolDescription = null,
        CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(id);
        var normalizedName = NormalizeName(name);

        if (await store.GetKnowledgeBaseAsync(normalizedId, ct) is not null)
        {
            throw new InvalidOperationException($"Knowledge base id '{normalizedId}' already exists.");
        }

        var knowledgeBase = new Models.KnowledgeBase
        {
            Id = normalizedId,
            Name = normalizedName,
            Description = NormalizeOptionalText(description),
            SearchToolDescription = NormalizeOptionalText(searchToolDescription),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await store.UpsertKnowledgeBaseAsync(knowledgeBase, ct);
        logger.LogInformation("Created knowledge base '{Name}' (Id: {Id}).", knowledgeBase.Name, knowledgeBase.Id);
        return knowledgeBase;
    }

    /// <summary>
    /// Updates knowledge-base display metadata.
    /// </summary>
    public async Task<Models.KnowledgeBase> UpdateAsync(
        string id,
        string name,
        string? description = null,
        CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(id);
        var knowledgeBase = await GetRequiredAsync(normalizedId, ct);
        var updated = knowledgeBase with
        {
            Name = NormalizeName(name),
            Description = NormalizeOptionalText(description)
        };

        await store.UpsertKnowledgeBaseAsync(updated, ct);
        logger.LogInformation("Updated knowledge base '{KnowledgeBaseId}'.", normalizedId);
        return updated;
    }

    /// <summary>
    /// Gets all knowledge bases.
    /// </summary>
    public Task<IReadOnlyList<Models.KnowledgeBase>> GetAllAsync(CancellationToken ct = default)
        => store.GetKnowledgeBasesAsync(ct);

    /// <summary>
    /// Gets one knowledge base by id.
    /// </summary>
    public Task<Models.KnowledgeBase?> GetByIdAsync(string id, CancellationToken ct = default)
        => store.GetKnowledgeBaseAsync(NormalizeId(id), ct);

    /// <summary>
    /// Gets one knowledge base or throws when it is missing.
    /// </summary>
    public async Task<Models.KnowledgeBase> GetRequiredAsync(string id, CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(id);
        return await store.GetKnowledgeBaseAsync(normalizedId, ct)
               ?? throw new KeyNotFoundException($"Knowledge base '{normalizedId}' was not found.");
    }

    /// <summary>
    /// Deletes a knowledge base inventory record.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await store.DeleteKnowledgeBaseAsync(NormalizeId(id), ct);
    }

    /// <summary>
    /// Gets the document inventory for a knowledge base.
    /// </summary>
    public async Task<IReadOnlyList<DocumentQueueItem>> GetDocumentInventoryAsync(
        string knowledgeBaseId,
        CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(knowledgeBaseId);
        _ = await GetRequiredAsync(normalizedId, ct);
        return await store.GetDocumentInventoryAsync(normalizedId, ct);
    }

    /// <summary>
    /// Removes one document inventory item.
    /// </summary>
    public async Task RemoveDocumentAsync(
        string knowledgeBaseId,
        string documentId,
        CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(knowledgeBaseId);
        _ = await GetRequiredAsync(normalizedId, ct);
        await store.DeleteDocumentAsync(normalizedId, documentId, ct);
        await sourceStore.DeleteContentAsync(normalizedId, documentId, ct);
    }

    /// <summary>
    /// Clears all document inventory items.
    /// </summary>
    public async Task<int> ClearDocumentsAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(knowledgeBaseId);
        var knowledgeBase = await GetRequiredAsync(normalizedId, ct);
        var removedCount = await store.DeleteDocumentsAsync(normalizedId, ct);
        await sourceStore.DeleteKnowledgeBaseAsync(normalizedId, ct);

        knowledgeBase.DocumentCount = 0;
        knowledgeBase.ChunkCount = 0;
        await store.UpsertKnowledgeBaseAsync(knowledgeBase, ct);
        return removedCount;
    }

    /// <summary>
    /// Imports markdown documents as pending knowledge-base inventory records.
    /// </summary>
    public async Task<KnowledgeBaseDocumentImportResult> ImportMarkdownDocumentsAsync(
        string knowledgeBaseId,
        string sourceGroupKey,
        IEnumerable<(string DocumentPath, string DocumentName)> documents,
        CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(knowledgeBaseId);
        _ = await GetRequiredAsync(normalizedId, ct);

        var normalizedGroupKey = NormalizeRequiredText(sourceGroupKey, nameof(sourceGroupKey));
        var states = documents
            .Where(static document => !string.IsNullOrWhiteSpace(document.DocumentPath))
            .GroupBy(static document => document.DocumentPath.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var document = group.First();
                var documentPath = document.DocumentPath.Trim();
                return CreatePendingDocumentState(
                    normalizedId,
                    documentPath,
                    document.DocumentName,
                    KnowledgeDocumentSourceKinds.Markdown,
                    normalizedGroupKey);
            })
            .ToList();

        return await store.AddPendingDocumentsAsync(normalizedId, states, ct);
    }

    /// <summary>
    /// Uploads one source document as a pending knowledge-base inventory record.
    /// </summary>
    public async Task UploadDocumentAsync(
        string knowledgeBaseId,
        string fileName,
        string content,
        CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(knowledgeBaseId);
        _ = await GetRequiredAsync(normalizedId, ct);

        var normalizedFileName = NormalizeRequiredText(fileName, nameof(fileName));
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Uploaded document content cannot be empty.", nameof(content));
        }

        var result = await store.AddPendingDocumentsAsync(
            normalizedId,
            [
                CreatePendingDocumentState(
                    normalizedId,
                    normalizedFileName,
                    normalizedFileName,
                    KnowledgeDocumentSourceKinds.Uploaded,
                    sourceGroupKey: null)
            ],
            ct);

        if (result.AddedCount == 0)
        {
            throw new InvalidOperationException(
                $"Document '{normalizedFileName}' already exists in knowledge base '{normalizedId}'.");
        }

        await sourceStore.SaveContentAsync(normalizedId, normalizedFileName, content, ct);
    }

    private static DocumentIndexState CreatePendingDocumentState(
        string knowledgeBaseId,
        string documentPath,
        string? documentName,
        string sourceKind,
        string? sourceGroupKey)
    {
        var resolvedName = string.IsNullOrWhiteSpace(documentName)
            ? ResolveDocumentName(documentPath)
            : documentName.Trim();

        return new DocumentIndexState
        {
            KnowledgeBaseId = knowledgeBaseId,
            DocumentPath = documentPath,
            DocumentName = resolvedName,
            Status = DocumentStatus.Pending,
            SourceKind = sourceKind,
            SourceGroupKey = sourceGroupKey,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string ResolveDocumentName(string documentPath)
    {
        var fileName = Path.GetFileName(documentPath);
        return string.IsNullOrWhiteSpace(fileName) ? documentPath : fileName;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Knowledge base name cannot be empty.", nameof(name));
        }

        return name.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeId(string id)
    {
        var normalized = KnowledgeBaseIdPolicy.Normalize(id);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Knowledge base id cannot be empty.", nameof(id));
        }

        if (!KnowledgeBaseIdPolicy.IsValid(normalized))
        {
            throw new ArgumentException(
                "Knowledge base id must use lowercase letters, numbers, and hyphens only, with a maximum length of 64 characters.",
                nameof(id));
        }

        return normalized;
    }
}
