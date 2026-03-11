using Monica.Markdown.UIMarkdown.Models;

namespace Monica.Markdown.Search;

internal sealed class LooseSubsequenceMarkdownDocumentSearchMatcher : IMarkdownDocumentSearchMatcher
{
    public EMarkdownDocumentSearchAlgorithm Algorithm => EMarkdownDocumentSearchAlgorithm.LooseSubsequence;

    public IReadOnlyList<MarkdownDocumentSearchCandidate> Search(
        MarkdownDocumentSearchGroupIndex groupIndex,
        string normalizedQuery,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var results = new List<MarkdownDocumentSearchCandidate>();

        foreach (var entry in groupIndex.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var section in entry.Sections)
            {
                var candidate = MatchSection(entry, section, normalizedQuery);
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
        string normalizedQuery)
    {
        var sources = new[]
        {
            new SearchSource(section.IsDocumentSection ? entry.NormalizedDocumentTitle : section.NormalizedTitle, 230, true),
            new SearchSource(section.NormalizedHeadingTrail, 180, true),
            new SearchSource(section.NormalizedText, 150, true),
            new SearchSource(entry.NormalizedRelativePath, 110, false)
        };

        SearchSource? bestSource = null;
        var bestSpan = new MarkdownSearchMatchSegment(0, 0);
        double bestCompactness = 0;
        double bestScore = double.MinValue;
        var hasSectionSpecificMatch = false;

        foreach (var source in sources)
        {
            if (!MarkdownDocumentSearchText.TryFindLooseSubsequence(source.Text, normalizedQuery, out var span, out var compactness))
            {
                continue;
            }

            if (source.IsSectionSpecific)
            {
                hasSectionSpecificMatch = true;
            }

            var score = source.Weight + compactness * 100 - span.Start * 0.03;
            if (score <= bestScore)
            {
                continue;
            }

            bestSource = source;
            bestSpan = span;
            bestCompactness = compactness;
            bestScore = score;
        }

        if (bestSource is null)
        {
            return null;
        }

        var resolvedSource = bestSource;

        if (!section.IsDocumentSection && !hasSectionSpecificMatch && !resolvedSource.IsSectionSpecific)
        {
            return null;
        }

        bestScore += section.IsDocumentSection ? 6 : 16;
        bestScore += bestCompactness * 20;

        return new MarkdownDocumentSearchCandidate(
            entry,
            section,
            resolvedSource.Text,
            [bestSpan],
            bestSpan,
            bestScore);
    }

    private sealed record SearchSource(string Text, double Weight, bool IsSectionSpecific);
}
