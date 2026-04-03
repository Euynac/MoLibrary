using System.Text;
using Markdig.Syntax;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace Monica.UI.Shared.Components.Markdown;

/// <summary>
/// Extends <see cref="MudMarkdown"/> with Mermaid fenced-code rendering.
/// </summary>
public class MoMudMarkdown : MudMarkdown
{
    private IReadOnlyList<MoMarkdownHeading> _headings = [];
    private int _tableOfContentsHeadingCount;

    /// <summary>
    /// Gets or sets whether Mermaid fenced code blocks should be rendered as diagrams.
    /// </summary>
    [Parameter]
    public bool EnableMermaid { get; set; } = true;

    /// <summary>
    /// Raised when the parsed markdown headings change.
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<MoMarkdownHeading>> HeadingsChanged { get; set; }

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);

        var headings = MoMarkdownHeadingParser.Parse(Value);
        _tableOfContentsHeadingCount = headings.Count(x => x.Level <= 3);

        if (_headings.SequenceEqual(headings))
        {
            return;
        }

        _headings = headings;

        if (HeadingsChanged.HasDelegate)
        {
            await HeadingsChanged.InvokeAsync(_headings);
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var originalHasTableOfContents = HasTableOfContents;
        HasTableOfContents = HasTableOfContents && _tableOfContentsHeadingCount > 0;

        try
        {
            base.BuildRenderTree(builder);
        }
        finally
        {
            HasTableOfContents = originalHasTableOfContents;
        }
    }

    protected override void RenderCodeBlock(in RenderTreeBuilder builder, ref int elementIndex, in CodeBlock code, in string? info)
    {
        if (EnableMermaid && string.Equals(info?.Trim(), "mermaid", StringComparison.OrdinalIgnoreCase))
        {
            builder.OpenComponent<MoMarkdownMermaidBlock>(0);
            builder.AddComponentParameter(1, nameof(MoMarkdownMermaidBlock.Definition), CreateCodeBlockText(code));
            builder.CloseComponent();
            elementIndex += 2;
            return;
        }

        base.RenderCodeBlock(builder, ref elementIndex, code, info);
    }

    private static string CreateCodeBlockText(CodeBlock code)
    {
        if (code.Lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var line in code.Lines)
        {
            if (builder.Length != 0)
            {
                builder.AppendLine();
            }

            builder.Append(line.ToString());
        }

        return builder.ToString();
    }
}
