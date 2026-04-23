using System.Text;
using Microsoft.Extensions.Options;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;
using Monica.Markdown.Services.Support;
using Monica.Modules;

namespace Monica.Markdown.Services;

/// <summary>
/// Builds and caches searchable markdown projections, then resolves document-level search hits.
/// </summary>
public class MarkdownDocumentSearchService(
    IMarkdownDocumentCatalog markdownService,
    IOptions<ModuleMarkdownOption> options) : IMarkdownDocumentSearcher
{
    private readonly ModuleMarkdownOption _option = options.Value;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private readonly Dictionary<string, MarkdownDocumentSearchGroupIndex> _groupIndexes = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly IMarkdownDocumentSearchMatcher[] _matchers =
    [
        new KeywordFuzzyMarkdownDocumentSearchMatcher(),
        new LooseSubsequenceMarkdownDocumentSearchMatcher()
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarkdownDocumentSearchResult>> SearchAsync(
        MarkdownDocumentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedQuery = MarkdownDocumentSearchText.Normalize(request.Query);
        if (normalizedQuery.Length < _option.DocumentSearchMinQueryLength)
        {
            return [];
        }

        var targetGroups = await ResolveTargetGroupsAsync(request, cancellationToken);
        if (targetGroups.Count == 0)
        {
            return [];
        }

        var matcher = ResolveMatcher();
        var candidates = new List<MarkdownDocumentSearchCandidate>();

        foreach (var group in targetGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var index = await GetOrBuildGroupIndexAsync(group, request.CurrentCulture, cancellationToken);
            candidates.AddRange(matcher.Search(index, normalizedQuery, cancellationToken));
        }

        return candidates
            .GroupBy(static candidate => candidate.Entry.Document.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static candidate => candidate.Score)
                .ThenBy(static candidate => candidate.Entry.Document.NavigationRelativePath, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.Entry.Document.Title, StringComparer.OrdinalIgnoreCase)
            .Take(_option.DocumentSearchMaxResults)
            .Select(candidate => BuildResult(candidate, _option.DocumentSearchPreviewLength))
            .ToArray();
    }

    private IMarkdownDocumentSearchMatcher ResolveMatcher()
    {
        return _matchers.FirstOrDefault(matcher => matcher.Algorithm == _option.DocumentSearchAlgorithm)
               ?? _matchers[0];
    }

    private async Task<List<MarkdownDocumentGroup>> ResolveTargetGroupsAsync(
        MarkdownDocumentSearchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var groups = await markdownService.GetAllDocumentGroupsAsync();
        var validGroups = groups
            .Where(static group => !group.HasConfigurationError)
            .ToList();

        if (request.IncludeAllKnowledgeBases || string.IsNullOrWhiteSpace(request.CurrentGroupKey))
        {
            return validGroups;
        }

        var currentGroup = validGroups.FirstOrDefault(group =>
            string.Equals(group.Key, request.CurrentGroupKey, StringComparison.OrdinalIgnoreCase));

        return currentGroup is null ? [] : [currentGroup];
    }

    private async Task<MarkdownDocumentSearchGroupIndex> GetOrBuildGroupIndexAsync(
        MarkdownDocumentGroup group,
        string? culture,
        CancellationToken cancellationToken)
    {
        var effectiveCulture = ResolveEffectiveCulture(group, culture);
        var indexKey = BuildIndexKey(group, effectiveCulture);
        var fingerprint = BuildFingerprint(group, effectiveCulture);
        if (_groupIndexes.TryGetValue(indexKey, out var cachedIndex)
            && string.Equals(cachedIndex.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return cachedIndex;
        }

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_groupIndexes.TryGetValue(indexKey, out cachedIndex)
                && string.Equals(cachedIndex.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedIndex;
            }

            var builtIndex = await BuildGroupIndexAsync(group, effectiveCulture, fingerprint, cancellationToken);
            _groupIndexes[indexKey] = builtIndex;
            return builtIndex;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private async Task<MarkdownDocumentSearchGroupIndex> BuildGroupIndexAsync(
        MarkdownDocumentGroup group,
        string? culture,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var documents = EnumerateDocuments(group, culture)
            .OrderBy(static document => document.NavigationRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var entries = new List<MarkdownDocumentSearchEntry>(documents.Length);

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await markdownService.GetDocumentContentAsync(document);
            var sections = MarkdownDocumentSearchContentParser.ParseSections(document, content);
            var pathTrail = BuildPathTrail(document.NavigationRelativePath);

            entries.Add(new MarkdownDocumentSearchEntry(
                group.Title,
                document,
                MarkdownDocumentSearchText.Normalize(document.Title),
                MarkdownDocumentSearchText.Normalize(document.NavigationRelativePath),
                pathTrail,
                MarkdownDocumentSearchText.Normalize(pathTrail),
                sections));
        }

        return new MarkdownDocumentSearchGroupIndex(group.Key, group.Title, fingerprint, entries);
    }

    private static IEnumerable<MarkdownDocument> EnumerateDocuments(
        MarkdownDocumentGroup group,
        string? culture)
    {
        return group.GetDocuments(culture);
    }

    private static string BuildIndexKey(MarkdownDocumentGroup group, string? culture)
    {
        return string.IsNullOrWhiteSpace(culture)
            ? group.Key
            : $"{group.Key}::{culture}";
    }

    private static string BuildFingerprint(MarkdownDocumentGroup group, string? culture)
    {
        var builder = new StringBuilder();

        foreach (var document in EnumerateDocuments(group, culture)
                     .OrderBy(static document => document.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(document.RelativePath)
                .Append('|')
                .Append(document.LastModifiedUtc.Ticks)
                .Append('|')
                .Append(document.FileSize)
                .Append(';');
        }

        return builder.ToString();
    }

    private static string? ResolveEffectiveCulture(MarkdownDocumentGroup group, string? culture)
    {
        return group.IsMultilingual
            ? group.ResolvePreferredLanguage(culture)
            : null;
    }

    private static string? BuildPathTrail(string relativePath)
    {
        var segments = relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length <= 1)
        {
            return null;
        }

        return string.Join(" / ", segments[..^1]);
    }

    private static MarkdownDocumentSearchResult BuildResult(
        MarkdownDocumentSearchCandidate candidate,
        int maxPreviewLength)
    {
        var preview = BuildPreview(candidate.SourceText, candidate.HighlightSegments, candidate.PrimarySegment, maxPreviewLength);
        var locator = BuildLocator(candidate);

        return new MarkdownDocumentSearchResult(
            candidate.Entry.Document.GroupKey,
            candidate.Entry.GroupTitle,
            candidate.Entry.Document.NavigationRelativePath,
            candidate.Entry.Document.Title,
            candidate.Entry.DocumentPathTrail,
            candidate.Section.IsDocumentSection ? null : candidate.Section.HeadingTrailText,
            preview.Text,
            preview.HighlightSegments,
            candidate.Section.AnchorId,
            locator,
            candidate.Score);
    }

    private static MarkdownSearchLocator BuildLocator(MarkdownDocumentSearchCandidate candidate)
    {
        var sourceText = candidate.SourceText;
        var primaryStart = Math.Clamp(candidate.PrimarySegment.Start, 0, Math.Max(sourceText.Length - 1, 0));
        var primaryLength = Math.Clamp(candidate.PrimarySegment.Length, 1, Math.Max(sourceText.Length - primaryStart, 1));
        var matchedText = SafeSubstring(sourceText, primaryStart, primaryLength);
        var prefixStart = Math.Max(0, primaryStart - 36);
        var suffixStart = primaryStart + primaryLength;
        var prefixContext = SafeSubstring(sourceText, prefixStart, primaryStart - prefixStart);
        var suffixContext = SafeSubstring(sourceText, suffixStart, Math.Min(36, sourceText.Length - suffixStart));

        return new MarkdownSearchLocator(
            candidate.Section.AnchorId,
            matchedText,
            prefixContext,
            suffixContext,
            candidate.Section.HeadingLevel);
    }

    private static PreviewData BuildPreview(
        string sourceText,
        IReadOnlyList<MarkdownSearchMatchSegment> sourceHighlights,
        MarkdownSearchMatchSegment primarySegment,
        int maxPreviewLength)
    {
        if (sourceText.Length <= maxPreviewLength)
        {
            return new PreviewData(sourceText, sourceHighlights);
        }

        var targetStart = Math.Max(0, primarySegment.Start - (maxPreviewLength / 3));
        var windowStart = ClampToBoundary(sourceText, targetStart, moveLeft: true);
        var windowEnd = Math.Min(sourceText.Length, windowStart + maxPreviewLength);
        windowEnd = ClampToBoundary(sourceText, windowEnd, moveLeft: false);

        if (windowEnd <= windowStart)
        {
            windowStart = Math.Max(0, Math.Min(primarySegment.Start, sourceText.Length - maxPreviewLength));
            windowEnd = Math.Min(sourceText.Length, windowStart + maxPreviewLength);
        }

        var rawPreview = sourceText[windowStart..windowEnd].Trim();
        var prefixEllipsis = windowStart > 0;
        var suffixEllipsis = windowEnd < sourceText.Length;

        var previewText = rawPreview;
        var offsetAdjustment = 0;

        if (prefixEllipsis)
        {
            previewText = "…" + previewText;
            offsetAdjustment = 1;
        }

        if (suffixEllipsis)
        {
            previewText += "…";
        }

        var previewHighlights = sourceHighlights
            .Select(segment => IntersectWithWindow(segment, windowStart, windowEnd))
            .Where(static segment => segment is not null)
            .Select(segment => new MarkdownSearchMatchSegment(
                segment!.Start - windowStart + offsetAdjustment,
                segment.Length))
            .ToArray();

        return new PreviewData(previewText, previewHighlights);
    }

    private static int ClampToBoundary(string sourceText, int position, bool moveLeft)
    {
        if (position <= 0 || position >= sourceText.Length)
        {
            return Math.Clamp(position, 0, sourceText.Length);
        }

        var index = position;

        if (moveLeft)
        {
            while (index > 0 && !char.IsWhiteSpace(sourceText[index - 1]))
            {
                index--;
            }

            return index;
        }

        while (index < sourceText.Length && !char.IsWhiteSpace(sourceText[index]))
        {
            index++;
        }

        return index;
    }

    private static MarkdownSearchMatchSegment? IntersectWithWindow(
        MarkdownSearchMatchSegment segment,
        int windowStart,
        int windowEnd)
    {
        var segmentStart = Math.Max(segment.Start, windowStart);
        var segmentEnd = Math.Min(segment.Start + segment.Length, windowEnd);

        return segmentEnd <= segmentStart
            ? null
            : new MarkdownSearchMatchSegment(segmentStart, segmentEnd - segmentStart);
    }

    private static string SafeSubstring(string text, int start, int length)
    {
        if (string.IsNullOrEmpty(text) || start >= text.Length || length <= 0)
        {
            return string.Empty;
        }

        var safeStart = Math.Max(0, start);
        var safeLength = Math.Min(length, text.Length - safeStart);
        return text.Substring(safeStart, safeLength);
    }

    private sealed record PreviewData(
        string Text,
        IReadOnlyList<MarkdownSearchMatchSegment> HighlightSegments);
}
