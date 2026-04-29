using Monica.AI.KnowledgeBase.Abstractions;
using Monica.AI.KnowledgeBase.Models;
using Monica.Markdown.Abstractions;

namespace Monica.AI.KnowledgeBase.Services;

/// <summary>
/// Default lookup-only service for knowledge-base listing, browsing, and content reads.
/// </summary>
public sealed class KnowledgeBaseLookupService(
    KnowledgeBaseService knowledgeBaseService,
    IKnowledgeDocumentSourceStore? sourceStore = null,
    IMarkdownDocumentCatalog? markdownService = null) : IKnowledgeBaseLookupService
{
    private const int DEFAULT_MAX_CHARACTERS = 8000;
    private const int MAX_MAX_CHARACTERS = 32000;

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeBaseSummary>> ListAsync(CancellationToken ct = default)
    {
        var knowledgeBases = await knowledgeBaseService.GetAllAsync(ct);
        return knowledgeBases.Select(ToSummary).ToList();
    }

    /// <inheritdoc />
    public async Task<KnowledgeBaseSummary?> GetSummaryAsync(string kbId, CancellationToken ct = default)
    {
        var knowledgeBase = await knowledgeBaseService.GetByIdAsync(kbId, ct);
        return knowledgeBase is null ? null : ToSummary(knowledgeBase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeDocumentSummary>> BrowseDocumentsAsync(
        string kbId,
        string? directoryPath = null,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        var normalizedDirectoryPath = NormalizeDirectoryPath(directoryPath);
        var inventory = await knowledgeBaseService.GetDocumentInventoryAsync(kbId, ct);
        return inventory
            .Where(item => MatchesDirectory(item.Id, normalizedDirectoryPath))
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxResults, 1, 500))
            .Select(ToDocumentSummary)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<KnowledgeDocumentTreeNode?> GetDocumentTreeAsync(
        string kbId,
        string? directoryPath = null,
        int maxDepth = 3,
        CancellationToken ct = default)
    {
        var normalizedDirectoryPath = NormalizeDirectoryPath(directoryPath);
        var documents = await BrowseDocumentsAsync(kbId, normalizedDirectoryPath, maxResults: 5000, ct);
        var root = new KnowledgeDocumentTreeNode
        {
            Name = string.IsNullOrWhiteSpace(normalizedDirectoryPath)
                ? kbId
                : Path.GetFileName(normalizedDirectoryPath),
            Path = normalizedDirectoryPath ?? string.Empty,
            IsDirectory = true
        };

        var effectiveMaxDepth = Math.Clamp(maxDepth, 1, 12);
        foreach (var document in documents)
        {
            AddDocument(root, document, normalizedDirectoryPath, effectiveMaxDepth);
        }

        return root;
    }

    /// <inheritdoc />
    public async Task<KnowledgeDocumentContent?> GetDocumentContentAsync(
        string kbId,
        string documentId,
        int maxCharacters = DEFAULT_MAX_CHARACTERS,
        int startCharacterIndex = 0,
        CancellationToken ct = default)
    {
        var inventory = await knowledgeBaseService.GetDocumentInventoryAsync(kbId, ct);
        var document = inventory.FirstOrDefault(item =>
            string.Equals(item.Id, documentId, StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            return null;
        }

        var content = await LoadDocumentContentAsync(kbId, document, ct);
        if (content is null)
        {
            return null;
        }

        var effectiveStart = Math.Clamp(startCharacterIndex, 0, content.Length);
        var effectiveMax = Math.Clamp(maxCharacters, 1, MAX_MAX_CHARACTERS);
        var returnedLength = Math.Min(effectiveMax, content.Length - effectiveStart);
        var segment = returnedLength > 0
            ? content.Substring(effectiveStart, returnedLength)
            : string.Empty;
        var nextStart = effectiveStart + returnedLength;

        return new KnowledgeDocumentContent(
            kbId,
            document.Id,
            document.Name,
            segment,
            effectiveStart,
            returnedLength,
            content.Length,
            nextStart < content.Length,
            nextStart < content.Length ? nextStart : null);
    }

    private static KnowledgeBaseSummary ToSummary(Models.KnowledgeBase knowledgeBase)
        => new(
            knowledgeBase.Id,
            knowledgeBase.Name,
            knowledgeBase.Description,
            knowledgeBase.DocumentCount,
            knowledgeBase.ChunkCount,
            IsRagEnabled(knowledgeBase));

    private static bool IsRagEnabled(Models.KnowledgeBase knowledgeBase)
        => !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingProviderId)
           && !string.IsNullOrWhiteSpace(knowledgeBase.EmbeddingModelName);

    private static KnowledgeDocumentSummary ToDocumentSummary(DocumentQueueItem item)
        => new(
            item.KnowledgeBaseId,
            item.Id,
            item.Name,
            NormalizeDirectoryPath(Path.GetDirectoryName(item.Id)) ?? string.Empty,
            item.Status.ToString(),
            item.ChunkCount,
            item.IndexedAt);

    private async Task<string?> LoadDocumentContentAsync(
        string kbId,
        DocumentQueueItem document,
        CancellationToken ct)
    {
        if (sourceStore is not null)
        {
            var storedContent = await sourceStore.GetContentAsync(kbId, document.Id, ct);
            if (storedContent is not null)
            {
                return storedContent;
            }
        }

        if (markdownService is null
            || !string.Equals(document.SourceKind, KnowledgeDocumentSourceKinds.Markdown, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(document.SourceGroupKey))
        {
            var groupDocuments = await markdownService.GetDocumentsAsync(document.SourceGroupKey);
            var matchedDocument = groupDocuments.FirstOrDefault(item =>
                string.Equals(item.RelativePath, document.Id, StringComparison.OrdinalIgnoreCase));
            if (matchedDocument is not null)
            {
                return await markdownService.GetDocumentContentAsync(matchedDocument);
            }
        }

        var markdownDocument = await markdownService.GetDocumentByPathAsync(document.Id);
        return await markdownService.GetDocumentContentAsync(markdownDocument);
    }

    private static string? NormalizeDirectoryPath(string? directoryPath)
    {
        var normalized = directoryPath?
            .Replace('\\', '/')
            .Trim()
            .Trim('/');

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool MatchesDirectory(string documentId, string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return true;
        }

        var normalizedDocumentId = documentId.Replace('\\', '/').TrimStart('/');
        return normalizedDocumentId.StartsWith(directoryPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddDocument(
        KnowledgeDocumentTreeNode root,
        KnowledgeDocumentSummary document,
        string? rootDirectoryPath,
        int maxDepth)
    {
        var path = document.DocumentId.Replace('\\', '/').Trim('/');
        if (!string.IsNullOrWhiteSpace(rootDirectoryPath)
            && path.StartsWith(rootDirectoryPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            path = path[(rootDirectoryPath.Length + 1)..];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var current = root;
        for (var i = 0; i < segments.Length; i++)
        {
            var isDocument = i == segments.Length - 1;
            if (!isDocument && i >= maxDepth)
            {
                return;
            }

            var segment = segments[i];
            var child = current.Children.FirstOrDefault(node =>
                string.Equals(node.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (child is null)
            {
                child = new KnowledgeDocumentTreeNode
                {
                    Name = segment,
                    Path = BuildChildPath(current.Path, segment),
                    IsDirectory = !isDocument,
                    DocumentId = isDocument ? document.DocumentId : null
                };
                current.Children.Add(child);
            }

            current = child;
        }
    }

    private static string BuildChildPath(string parentPath, string childName)
        => string.IsNullOrWhiteSpace(parentPath) ? childName : $"{parentPath}/{childName}";
}
