using System.Text.RegularExpressions;
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
    private const int CONTENT_PREVIEW_RADIUS = 180;
    private const int REGEX_TIMEOUT_MILLISECONDS = 500;

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
    public async Task<IReadOnlyList<KnowledgeDocumentSearchResult>> SearchDocumentsAsync(
        string kbId,
        string query,
        KnowledgeDocumentSearchMode mode = KnowledgeDocumentSearchMode.Fuzzy,
        string? directoryPath = null,
        bool includeContent = true,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = NormalizeSearchText(query);
        var normalizedDirectoryPath = NormalizeDirectoryPath(directoryPath);
        var inventory = await knowledgeBaseService.GetDocumentInventoryAsync(kbId, ct);
        var documents = inventory
            .Where(item => MatchesDirectory(item.Id, normalizedDirectoryPath))
            .ToList();

        var regex = mode == KnowledgeDocumentSearchMode.Regex
            ? CreateSearchRegex(normalizedQuery)
            : null;
        var tokens = mode == KnowledgeDocumentSearchMode.Fuzzy
            ? SplitSearchTokens(normalizedQuery)
            : [];
        var results = new List<KnowledgeDocumentSearchResult>();

        foreach (var document in documents)
        {
            ct.ThrowIfCancellationRequested();

            var content = includeContent
                ? await LoadDocumentContentAsync(kbId, document, ct)
                : null;
            var result = mode == KnowledgeDocumentSearchMode.Regex
                ? MatchByRegex(document, regex!, content)
                : MatchByFuzzy(document, normalizedQuery, tokens, content);

            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results
            .OrderByDescending(static result => result.Score)
            .ThenBy(static result => result.DocumentId, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxResults, 1, 200))
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

    private static KnowledgeDocumentSearchResult? MatchByFuzzy(
        DocumentQueueItem document,
        string normalizedQuery,
        IReadOnlyList<string> tokens,
        string? content)
    {
        if (tokens.Count == 0)
        {
            return null;
        }

        var summary = ToDocumentSummary(document);
        var fields = new List<SearchField>
        {
            new SearchField(summary.Name, KnowledgeDocumentSearchMatchField.Name, 240d),
            new SearchField(summary.DocumentId, KnowledgeDocumentSearchMatchField.DocumentId, 210d),
            new SearchField(summary.DirectoryPath, KnowledgeDocumentSearchMatchField.DirectoryPath, 160d)
        };
        if (content is not null)
        {
            fields.Add(new SearchField(content, KnowledgeDocumentSearchMatchField.Content, 120d));
        }

        KnowledgeDocumentSearchResult? best = null;
        foreach (var field in fields)
        {
            var match = ScoreFuzzyField(summary, field, normalizedQuery, tokens, content is not null);
            if (match is not null && (best is null || match.Score > best.Score))
            {
                best = match;
            }
        }

        return best;
    }

    private static KnowledgeDocumentSearchResult? ScoreFuzzyField(
        KnowledgeDocumentSummary summary,
        SearchField field,
        string normalizedQuery,
        IReadOnlyList<string> tokens,
        bool contentAvailable)
    {
        if (string.IsNullOrWhiteSpace(field.Text))
        {
            return null;
        }

        var exactIndex = field.Text.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
        var matchedTokens = tokens
            .Select(token => new
            {
                Token = token,
                Index = field.Text.IndexOf(token, StringComparison.OrdinalIgnoreCase)
            })
            .Where(static token => token.Index >= 0)
            .ToList();

        if (exactIndex < 0 && matchedTokens.Count == 0)
        {
            return null;
        }

        var firstIndex = exactIndex >= 0
            ? exactIndex
            : matchedTokens.Min(static token => token.Index);
        var matchLength = exactIndex >= 0
            ? normalizedQuery.Length
            : matchedTokens
                .OrderBy(static token => token.Index)
                .First()
                .Token
                .Length;
        var score = field.Weight;
        if (exactIndex >= 0)
        {
            score += 120;
        }

        score += matchedTokens.Count * 40d / tokens.Count;
        score -= Math.Min(firstIndex, 500) * 0.04;

        return BuildSearchResult(
            summary,
            KnowledgeDocumentSearchMode.Fuzzy,
            field.MatchField,
            score,
            field.Text,
            firstIndex,
            matchLength,
            contentSearched: field.MatchField == KnowledgeDocumentSearchMatchField.Content,
            contentAvailable);
    }

    private static KnowledgeDocumentSearchResult? MatchByRegex(
        DocumentQueueItem document,
        Regex regex,
        string? content)
    {
        var summary = ToDocumentSummary(document);
        var fields = new List<SearchField>
        {
            new SearchField(summary.Name, KnowledgeDocumentSearchMatchField.Name, 240d),
            new SearchField(summary.DocumentId, KnowledgeDocumentSearchMatchField.DocumentId, 210d),
            new SearchField(summary.DirectoryPath, KnowledgeDocumentSearchMatchField.DirectoryPath, 160d)
        };
        if (content is not null)
        {
            fields.Add(new SearchField(content, KnowledgeDocumentSearchMatchField.Content, 120d));
        }

        KnowledgeDocumentSearchResult? best = null;
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Text))
            {
                continue;
            }

            var match = regex.Match(field.Text);
            if (!match.Success)
            {
                continue;
            }

            var score = field.Weight + 100 - Math.Min(match.Index, 500) * 0.04;
            var result = BuildSearchResult(
                summary,
                KnowledgeDocumentSearchMode.Regex,
                field.MatchField,
                score,
                field.Text,
                match.Index,
                match.Length,
                contentSearched: field.MatchField == KnowledgeDocumentSearchMatchField.Content,
                contentAvailable: content is not null);

            if (best is null || result.Score > best.Score)
            {
                best = result;
            }
        }

        return best;
    }

    private static KnowledgeDocumentSearchResult BuildSearchResult(
        KnowledgeDocumentSummary summary,
        KnowledgeDocumentSearchMode mode,
        KnowledgeDocumentSearchMatchField matchField,
        double score,
        string matchedText,
        int matchStart,
        int matchLength,
        bool contentSearched,
        bool contentAvailable)
    {
        return new KnowledgeDocumentSearchResult(
            summary.KnowledgeBaseId,
            summary.DocumentId,
            summary.Name,
            summary.DirectoryPath,
            summary.Status,
            summary.ChunkCount,
            summary.IndexedAt,
            mode,
            matchField,
            score,
            BuildPreview(matchedText, matchStart, matchLength),
            matchStart,
            matchLength,
            contentSearched,
            contentAvailable);
    }

    private static string BuildPreview(string text, int matchStart, int matchLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var start = Math.Max(0, matchStart - CONTENT_PREVIEW_RADIUS);
        var end = Math.Min(text.Length, matchStart + Math.Max(matchLength, 1) + CONTENT_PREVIEW_RADIUS);
        var preview = text[start..end].Trim();

        return string.Concat(
            start > 0 ? "..." : string.Empty,
            preview,
            end < text.Length ? "..." : string.Empty);
    }

    private static Regex CreateSearchRegex(string pattern)
    {
        try
        {
            return new Regex(
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(REGEX_TIMEOUT_MILLISECONDS));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Invalid knowledge-base search regex: {ex.Message}", ex);
        }
    }

    private static IReadOnlyList<string> SplitSearchTokens(string normalizedQuery)
    {
        return normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed record SearchField(
        string Text,
        KnowledgeDocumentSearchMatchField MatchField,
        double Weight);
}
