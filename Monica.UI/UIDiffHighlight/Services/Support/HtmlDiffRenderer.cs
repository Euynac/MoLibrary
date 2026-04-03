using System.Text;
using System.Web;
using Monica.UI.UIDiffHighlight.Abstractions;
using Monica.UI.UIDiffHighlight.Models;

namespace Monica.UI.UIDiffHighlight.Services.Support;

/// <summary>
/// HTML diff renderer with a GitHub-style table layout.
/// </summary>
public class HtmlDiffRenderer : IDiffHighlightRenderer
{
    public EDiffOutputFormat SupportedFormat => EDiffOutputFormat.Html;
    
    /// <summary>
    /// Renders diff lines as HTML.
    /// </summary>
    public string Render(IEnumerable<DiffLine> lines, DiffHighlightStyle style)
    {
        var html = new StringBuilder();
        
        // Open the container and optionally inject the built-in stylesheet.
        html.AppendLine($"<div class=\"{style.ContainerStyle}\">");
        
        if (style.IncludeDefaultCss)
        {
            html.AppendLine(GetDefaultCssStyles());
        }
        
        // Render the diff rows in a simple table layout.
        html.AppendLine("<table class=\"diff-table\">");
        
        foreach (var line in lines)
        {
            html.AppendLine(RenderLine(line, style));
        }
        
        html.AppendLine("</table>");
        html.AppendLine("</div>");
        
        return html.ToString();
    }
    
    /// <summary>
    /// Renders a single diff line as a table row.
    /// </summary>
    public string RenderLine(DiffLine line, DiffHighlightStyle style)
    {
        var html = new StringBuilder();
        var lineClass = GetLineStyleClass(line.Type, style);
        var lineTypeSymbol = GetLineTypeSymbol(line.Type);
        
        html.AppendLine($"<tr class=\"{lineClass}\">");
        
        // Render old and new line numbers in separate columns.
        html.Append($"<td class=\"{style.LineNumberStyle} old-line-number\">");
        if (line.OldLineNumber > 0)
        {
            html.Append(line.OldLineNumber);
        }
        html.AppendLine("</td>");
        
        html.Append($"<td class=\"{style.LineNumberStyle} new-line-number\">");
        if (line.NewLineNumber > 0)
        {
            html.Append(line.NewLineNumber);
        }
        html.AppendLine("</td>");
        
        // Render the diff marker column.
        html.AppendLine($"<td class=\"diff-symbol\">{lineTypeSymbol}</td>");
        
        // Render the content column, including inline highlights for modifications.
        html.Append("<td class=\"diff-content\">");
        html.Append(RenderLineContent(line, style));
        html.AppendLine("</td>");
        
        html.AppendLine("</tr>");
        
        return html.ToString();
    }
    
    /// <summary>
    /// Renders the visible content for a diff line.
    /// </summary>
    private string RenderLineContent(DiffLine line, DiffHighlightStyle style)
    {
        string content;
        
        switch (line.Type)
        {
            case EDiffLineType.Added:
                content = HttpUtility.HtmlEncode(line.NewContent);
                break;
            case EDiffLineType.Deleted:
                content = HttpUtility.HtmlEncode(line.OldContent);
                break;
            case EDiffLineType.Modified:
                content = RenderModifiedLineContent(line, style);
                break;
            default:
                content = HttpUtility.HtmlEncode(line.NewContent ?? line.OldContent);
                break;
        }
        
        return content;
    }
    
    /// <summary>
    /// Renders a modified line with character-level highlights.
    /// </summary>
    private string RenderModifiedLineContent(DiffLine line, DiffHighlightStyle style)
    {
        if (line.CharacterDiffs == null || !line.CharacterDiffs.Any())
        {
            return HttpUtility.HtmlEncode(line.NewContent);
        }
        
        var html = new StringBuilder();
        var newContent = line.NewContent;
        var oldContent = line.OldContent;
        
        // Render the old content with deleted segments emphasized.
        html.Append("<div class=\"diff-old-content\">");
        int oldPos = 0;
        
        foreach (var charDiff in line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Deleted))
        {
            // Write unchanged text before the next deleted span.
            if (oldPos < charDiff.Start)
            {
                html.Append(HttpUtility.HtmlEncode(oldContent.Substring(oldPos, charDiff.Start - oldPos)));
            }
            
            // Highlight the deleted span.
            html.Append($"<span class=\"{style.DeletedCharacterStyle}\">");
            html.Append(HttpUtility.HtmlEncode(charDiff.Content));
            html.Append("</span>");
            
            oldPos = charDiff.Start + charDiff.Length;
        }
        
        // Append any unchanged suffix after the last deleted span.
        if (oldPos < oldContent.Length)
        {
            html.Append(HttpUtility.HtmlEncode(oldContent.Substring(oldPos)));
        }
        
        html.AppendLine("</div>");
        
        // Render the new content with added segments emphasized.
        html.Append("<div class=\"diff-new-content\">");
        int newPos = 0;
        
        foreach (var charDiff in line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Added))
        {
            // Write unchanged text before the next added span.
            if (newPos < charDiff.Start)
            {
                html.Append(HttpUtility.HtmlEncode(newContent.Substring(newPos, charDiff.Start - newPos)));
            }
            
            // Highlight the added span.
            html.Append($"<span class=\"{style.AddedCharacterStyle}\">");
            html.Append(HttpUtility.HtmlEncode(charDiff.Content));
            html.Append("</span>");
            
            newPos = charDiff.Start + charDiff.Length;
        }
        
        // Append any unchanged suffix after the last added span.
        if (newPos < newContent.Length)
        {
            html.Append(HttpUtility.HtmlEncode(newContent.Substring(newPos)));
        }
        
        html.Append("</div>");
        
        return html.ToString();
    }
    
    /// <summary>
    /// Gets the CSS class for the current diff line type.
    /// </summary>
    private string GetLineStyleClass(EDiffLineType type, DiffHighlightStyle style)
    {
        return type switch
        {
            EDiffLineType.Added => style.AddedLineStyle,
            EDiffLineType.Deleted => style.DeletedLineStyle,
            EDiffLineType.Modified => style.ModifiedLineStyle,
            EDiffLineType.Unchanged => style.UnchangedLineStyle,
            _ => style.UnchangedLineStyle
        };
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
    /// Gets the built-in CSS used by the HTML renderer.
    /// </summary>
    private string GetDefaultCssStyles()
    {
        return @"
<style>
.diff-container {
    font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
    font-size: 12px;
    line-height: 1.4;
    border: 1px solid #d1d9e0;
    border-radius: 8px;
    overflow: hidden;
}

.diff-table {
    width: 100%;
    border-collapse: collapse;
    background-color: #ffffff;
}

.diff-table tr:hover {
    background-color: rgba(0, 0, 0, 0.03);
}

.diff-line-number {
    width: 40px;
    padding: 2px 8px;
    text-align: right;
    color: #656d76;
    background-color: #f6f8fa;
    border-right: 1px solid #d1d9e0;
    user-select: none;
    vertical-align: top;
}

.diff-symbol {
    width: 20px;
    padding: 2px 8px;
    text-align: center;
    font-weight: bold;
    user-select: none;
    vertical-align: top;
}

.diff-content {
    padding: 2px 8px;
    white-space: pre;
    vertical-align: top;
}

.diff-added {
    background-color: #ccffd8;
}

.diff-added .diff-symbol {
    color: #1a7f37;
    background-color: #ccffd8;
}

.diff-deleted {
    background-color: #ffd7d5;
}

.diff-deleted .diff-symbol {
    color: #cf222e;
    background-color: #ffd7d5;
}

.diff-modified {
    background-color: #fff8c5;
}

.diff-modified .diff-symbol {
    color: #bf8700;
    background-color: #fff8c5;
}

.diff-unchanged {
    background-color: #ffffff;
}

.diff-added-char {
    background-color: #abf2bc;
    color: #1a7f37;
    font-weight: bold;
}

.diff-deleted-char {
    background-color: #ffc1cc;
    color: #cf222e;
    font-weight: bold;
    text-decoration: line-through;
}

.diff-old-content {
    opacity: 0.7;
}

.diff-new-content {
    margin-top: 2px;
}
</style>";
    }
}
