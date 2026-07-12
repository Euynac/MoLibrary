using Microsoft.Extensions.Logging;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.KnowledgeBase.Services;

/// <summary>
/// Owns knowledge-base identity, metadata, aggregate statistics, and deletion orchestration.
/// </summary>
internal sealed class KnowledgeBaseService(
    IKnowledgeBaseStore store,
    IKnowledgeDocumentSourceStore sourceStore,
    IEnumerable<IKnowledgeBaseLifecycleHandler> lifecycleHandlers,
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
        if (await store.GetKnowledgeBaseAsync(normalizedId, ct) is not null)
        {
            throw new InvalidOperationException($"Knowledge base id '{normalizedId}' already exists.");
        }

        var knowledgeBase = new Models.KnowledgeBase
        {
            Id = normalizedId,
            Name = NormalizeName(name),
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
        var knowledgeBase = await GetRequiredAsync(id, ct);
        var updated = knowledgeBase with
        {
            Name = NormalizeName(name),
            Description = NormalizeOptionalText(description)
        };

        await store.UpsertKnowledgeBaseAsync(updated, ct);
        logger.LogInformation("Updated knowledge base '{KnowledgeBaseId}'.", updated.Id);
        return updated;
    }

    /// <summary>
    /// Returns all knowledge bases with current aggregate counts.
    /// </summary>
    public async Task<IReadOnlyList<Models.KnowledgeBase>> GetAllAsync(CancellationToken ct = default)
    {
        var knowledgeBases = await store.GetKnowledgeBasesAsync(ct);
        foreach (var knowledgeBase in knowledgeBases)
        {
            await RefreshStatisticsAsync(knowledgeBase, ct);
        }

        return knowledgeBases;
    }

    /// <summary>
    /// Returns one knowledge base with current aggregate counts.
    /// </summary>
    public async Task<Models.KnowledgeBase?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var knowledgeBase = await store.GetKnowledgeBaseAsync(NormalizeId(id), ct);
        if (knowledgeBase is not null)
        {
            await RefreshStatisticsAsync(knowledgeBase, ct);
        }

        return knowledgeBase;
    }

    /// <summary>
    /// Returns one knowledge base or throws when it does not exist.
    /// </summary>
    public async Task<Models.KnowledgeBase> GetRequiredAsync(string id, CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(id);
        var knowledgeBase = await store.GetKnowledgeBaseAsync(normalizedId, ct)
                            ?? throw new KeyNotFoundException($"Knowledge base '{normalizedId}' was not found.");

        await RefreshStatisticsAsync(knowledgeBase, ct);
        return knowledgeBase;
    }

    /// <summary>
    /// Deletes a knowledge base after optional features release their owned resources.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var normalizedId = NormalizeId(id);
        _ = await GetRequiredAsync(normalizedId, ct);
        foreach (var handler in lifecycleHandlers)
        {
            await handler.OnKnowledgeBaseDeletingAsync(normalizedId, ct);
        }

        await sourceStore.DeleteKnowledgeBaseAsync(normalizedId, ct);
        await store.DeleteKnowledgeBaseAsync(normalizedId, ct);
        logger.LogInformation("Deleted knowledge base '{KnowledgeBaseId}'.", normalizedId);
    }

    private async Task RefreshStatisticsAsync(Models.KnowledgeBase knowledgeBase, CancellationToken ct)
    {
        var documents = await store.GetDocumentInventoryAsync(knowledgeBase.Id, ct);
        var documentCount = documents.Count;
        var chunkCount = documents.Sum(static document => Math.Max(0, document.ChunkCount));
        if (knowledgeBase.DocumentCount == documentCount && knowledgeBase.ChunkCount == chunkCount)
        {
            return;
        }

        knowledgeBase.DocumentCount = documentCount;
        knowledgeBase.ChunkCount = chunkCount;
        await store.UpsertKnowledgeBaseAsync(knowledgeBase, ct);
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
