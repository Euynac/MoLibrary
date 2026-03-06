using System.Text;
using Markdig.Syntax;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace Monica.UI.Components.Markdown;

/// <summary>
/// Extends <see cref="MudMarkdown"/> with Mermaid fenced-code rendering.
/// </summary>
public class MoMudMarkdown : MudMarkdown
{
    /// <summary>
    /// Gets or sets whether Mermaid fenced code blocks should be rendered as diagrams.
    /// </summary>
    [Parameter]
    public bool EnableMermaid { get; set; } = true;

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
