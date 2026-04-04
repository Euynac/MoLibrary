using Monica.Markdown.Models;

namespace Monica.Markdown.Services.Support;

internal sealed class KeywordFuzzyMarkdownDocumentSearchMatcher : IMarkdownDocumentSearchMatcher
{
    public MarkdownSearchAlgorithm Algorithm => MarkdownSearchAlgorithm.KeywordFuzzy;

    public IReadOnlyList<MarkdownDocumentSearchCandidate> Search(
        MarkdownDocumentSearchGroupIndex groupIndex,
        string normalizedQuery,
        CancellationToken cancellationToken)
    {
        var tokens = MarkdownDocumentSearchText.SplitKeywords(normalizedQuery);
        if (tokens.Count == 0)
        {
            return [];
        }

        var results = new List<MarkdownDocumentSearchCandidate>();

        foreach (var entry in groupIndex.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var section in entry.Sections)
            {
                var candidate = MatchSection(entry, section, normalizedQuery, tokens);
                if (candidate is not null)
                {
                    results.Add(candidate);
                }
            }
        }

        return results;
    }

    private static MarkdownDocumentSearchCandidate? MatchSection(
        MarkdownDocumentSearchEntry entry,
        MarkdownDocumentSearchSection section,
        string normalizedQuery,
        IReadOnlyList<string> tokens)
    {
        var titleText = section.IsDocumentSection ? entry.NormalizedDocumentTitle : section.NormalizedTitle;
        var pathText = entry.NormalizedRelativePath;
        var trailText = section.NormalizedHeadingTrail;
        var contentText = section.NormalizedText;

        var anySectionSpecificMatch = false;

        foreach (var token in tokens)
        {
            var matchesTitle = MarkdownDocumentSearchText.ContainsIgnoreCase(titleText, token);
            var matchesPath = MarkdownDocumentSearchText.ContainsIgnoreCase(pathText, token);
            var matchesTrail = MarkdownDocumentSearchText.ContainsIgnoreCase(trailText, token);
            var matchesContent = MarkdownDocumentSearchText.ContainsIgnoreCase(contentText, token);

            if (!matchesTitle && !matchesPath && !matchesTrail && !matchesContent)
            {
                return null;
            }

            if (matchesTitle || matchesTrail || matchesContent)
            {
                anySectionSpecificMatch = true;
            }
        }

        if (!section.IsDocumentSection && !anySectionSpecificMatch)
        {
            return null;
        }

        var titleSegments = MarkdownDocumentSearchText.FindAllSegments(titleText, tokens);
        var trailSegments = MarkdownDocumentSearchText.FindAllSegments(trailText, tokens);
        var contentSegments = MarkdownDocumentSearchText.FindAllSegments(contentText, tokens);
        var pathSegments = MarkdownDocumentSearchText.FindAllSegments(pathText, tokens);

        var exactContentIndex = MarkdownDocumentSearchText.IndexOfIgnoreCase(contentText, normalizedQuery);
        var exactTrailIndex = MarkdownDocumentSearchText.IndexOfIgnoreCase(trailText, normalizedQuery);
        var exactTitleIndex = MarkdownDocumentSearchText.IndexOfIgnoreCase(titleText, normalizedQuery);
        var exactPathIndex = MarkdownDocumentSearchText.IndexOfIgnoreCase(pathText, normalizedQuery);

        var score = 0d;

        if (exactTitleIndex >= 0)
        {
            score += 240;
        }

        if (exactTrailIndex >= 0)
        {
            score += 190;
        }

        if (exactContentIndex >= 0)
        {
            score += 170;
        }

        if (exactPathIndex >= 0)
        {
            score += 120;
        }

        foreach (var token in tokens)
        {
            if (MarkdownDocumentSearchText.ContainsIgnoreCase(titleText, token))
            {
                score += 60;
            }

            if (MarkdownDocumentSearchText.ContainsIgnoreCase(trailText, token))
            {
                score += 42;
            }

            if (MarkdownDocumentSearchText.ContainsIgnoreCase(pathText, token))
            {
                score += 30;
            }

            if (MarkdownDocumentSearchText.ContainsIgnoreCase(contentText, token))
            {
                score += 22;
            }
        }

        string sourceText;
        IReadOnlyList<MarkdownSearchMatchSegment> highlightSegments;
        MarkdownSearchMatchSegment primarySegment;

        if (contentSegments.Count > 0)
        {
            sourceText = contentText;
            highlightSegments = contentSegments;
            primarySegment = exactContentIndex >= 0
                ? new MarkdownSearchMatchSegment(exactContentIndex, normalizedQuery.Length)
                : contentSegments[0];
        }
        else if (trailSegments.Count > 0)
        {
            sourceText = trailText;
            highlightSegments = trailSegments;
            primarySegment = exactTrailIndex >= 0
                ? new MarkdownSearchMatchSegment(exactTrailIndex, normalizedQuery.Length)
                : trailSegments[0];
        }
        else if (titleSegments.Count > 0)
        {
            sourceText = titleText;
            highlightSegments = titleSegments;
            primarySegment = exactTitleIndex >= 0
                ? new MarkdownSearchMatchSegment(exactTitleIndex, normalizedQuery.Length)
                : titleSegments[0];
        }
        else if (pathSegments.Count > 0)
        {
            sourceText = pathText;
            highlightSegments = pathSegments;
            primarySegment = exactPathIndex >= 0
                ? new MarkdownSearchMatchSegment(exactPathIndex, normalizedQuery.Length)
                : pathSegments[0];
        }
        else
        {
            return null;
        }

        score -= primarySegment.Start * 0.02;
        score += section.IsDocumentSection ? 8 : 18;

        return new MarkdownDocumentSearchCandidate(
            entry,
            section,
            sourceText,
            highlightSegments,
            primarySegment,
            score);
    }
}
