using System.Text;
using Monica.UI.UIDiffHighlight.Abstractions;
using Monica.UI.UIDiffHighlight.Models;

namespace Monica.UI.UIDiffHighlight.Services.Support;

/// <summary>
/// Markdown diff renderer.
/// </summary>
public class MarkdownDiffRenderer : IDiffHighlightRenderer
{
    public EDiffOutputFormat SupportedFormat => EDiffOutputFormat.Markdown;
    
    /// <summary>
    /// Renders diff lines as Markdown.
    /// </summary>
    public string Render(IEnumerable<DiffLine> lines, DiffHighlightStyle style)
    {
        var markdown = new StringBuilder();
        
        // Use a diff fenced block so Markdown viewers can style the output.
        markdown.AppendLine("```diff");
        
        foreach (var line in lines)
        {
            markdown.AppendLine(RenderLine(line, style));
        }
        
        markdown.AppendLine("```");
        
        return markdown.ToString();
    }
    
    /// <summary>
    /// Renders a single diff line.
    /// </summary>
    public string RenderLine(DiffLine line, DiffHighlightStyle style)
    {
        var symbol = GetLineTypeSymbol(line.Type);
        var content = GetLineContent(line);
        
        return $"{symbol} {content}";
    }
    
    /// <summary>
    /// Gets the display marker for a diff line type.
    /// </summary>
    private string GetLineTypeSymbol(EDiffLineType type)
    {
        return type switch
        {
            EDiffLineType.Added => "+",
            EDiffLineType.Deleted => "-",
            EDiffLineType.Modified => "~",
            EDiffLineType.Unchanged => " ",
            _ => " "
        };
    }
    
    /// <summary>
    /// Gets the visible content for a diff line.
    /// </summary>
    private string GetLineContent(DiffLine line)
    {
        return line.Type switch
        {
            EDiffLineType.Added => line.NewContent,
            EDiffLineType.Deleted => line.OldContent,
            EDiffLineType.Modified => $"{line.OldContent} -> {line.NewContent}",
            _ => line.NewContent ?? line.OldContent
        };
    }
}
