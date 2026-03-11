using System.Net;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Monica.UI.Components.Markdown;

internal static class MoMarkdownHeadingParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static IReadOnlyList<MoMarkdownHeading> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        if (document.Count == 0)
        {
            return [];
        }

        var headings = new List<MoMarkdownHeading>();
        CollectHeadings(document, headings);
        return headings;
    }

    private static void CollectHeadings(ContainerBlock container, ICollection<MoMarkdownHeading> headings)
    {
        foreach (var block in container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                {
                    var parsedHeading = CreateHeading(heading);
                    if (parsedHeading is not null)
                    {
                        headings.Add(parsedHeading);
                    }

                    break;
                }
                case ContainerBlock nestedContainer:
                    CollectHeadings(nestedContainer, headings);
                    break;
            }
        }
    }

    private static MoMarkdownHeading? CreateHeading(HeadingBlock heading)
    {
        if (heading.Inline is null)
        {
            return null;
        }

        var idBuilder = new StringBuilder();
        var textBuilder = new StringBuilder();

        foreach (var inline in heading.Inline)
        {
            if (inline is not LiteralInline literalInline || literalInline.Content.IsEmpty)
            {
                continue;
            }

            var text = literalInline.Content.ToString();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            textBuilder.Append(text);
            AppendHeadingIdSegment(idBuilder, text);
        }

        if (idBuilder.Length == 0 || textBuilder.Length == 0)
        {
            return null;
        }

        return new MoMarkdownHeading(
            WebUtility.UrlEncode(idBuilder.ToString()),
            textBuilder.ToString(),
            heading.Level);
    }

    private static void AppendHeadingIdSegment(StringBuilder builder, string text)
    {
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            var spaceIndex = remaining.IndexOf(' ');
            if (spaceIndex < 0)
            {
                AppendLowerInvariant(builder, remaining);
                break;
            }

            if (spaceIndex > 0)
            {
                AppendLowerInvariant(builder, remaining[..spaceIndex]);
                builder.Append('-');
            }

            remaining = remaining[(spaceIndex + 1)..];
        }
    }

    private static void AppendLowerInvariant(StringBuilder builder, ReadOnlySpan<char> segment)
    {
        foreach (var character in segment)
        {
            builder.Append(char.ToLowerInvariant(character));
        }
    }
}
