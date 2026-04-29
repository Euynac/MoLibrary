using Microsoft.Extensions.Logging;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Services;

/// <summary>
/// Coordinates knowledge-base metadata and inventory operations.
/// </summary>
public sealed class KnowledgeBaseService(
    IKnowledgeBaseStore store,
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
    }

    /// <summary>
    /// Clears all document inventory items.
    /// </summary>
    public async Task<int> ClearDocumentsAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(knowledgeBaseId);
        var knowledgeBase = await GetRequiredAsync(normalizedId, ct);
        var removedCount = await store.DeleteDocumentsAsync(normalizedId, ct);

        knowledgeBase.DocumentCount = 0;
        knowledgeBase.ChunkCount = 0;
        await store.UpsertKnowledgeBaseAsync(knowledgeBase, ct);
        return removedCount;
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
