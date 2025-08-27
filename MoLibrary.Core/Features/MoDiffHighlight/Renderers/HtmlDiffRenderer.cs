using System.Text;
using System.Web;
using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight.Renderers;

/// <summary>
/// HTML 差异渲染器（类似 GitHub 样式）
/// </summary>
public class HtmlDiffRenderer : IDiffHighlightRenderer
{
    public EDiffOutputFormat SupportedFormat => EDiffOutputFormat.Html;
    
    /// <summary>
    /// 渲染差异结果为 HTML 格式
    /// </summary>
    public string Render(IEnumerable<DiffLine> lines, DiffHighlightStyle style)
    {
        var html = new StringBuilder();
        
        // 添加容器开始标签和默认样式
        html.AppendLine($"<div class=\"{style.ContainerStyle}\">");
        
        if (style.IncludeDefaultCss)
        {
            html.AppendLine(GetDefaultCssStyles());
        }
        
        // 添加表格结构
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
    /// 渲染单行差异
    /// </summary>
    public string RenderLine(DiffLine line, DiffHighlightStyle style)
    {
        var html = new StringBuilder();
        var lineClass = GetLineStyleClass(line.Type, style);
        var lineTypeSymbol = GetLineTypeSymbol(line.Type);
        
        html.AppendLine($"<tr class=\"{lineClass}\">");
        
        // 行号列
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
        
        // 符号列
        html.AppendLine($"<td class=\"diff-symbol\">{lineTypeSymbol}</td>");
        
        // 内容列
        html.Append("<td class=\"diff-content\">");
        html.Append(RenderLineContent(line, style));
        html.AppendLine("</td>");
        
        html.AppendLine("</tr>");
        
        return html.ToString();
    }
    
    /// <summary>
    /// 渲染行内容，包括字符级差异
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
    /// 渲染修改行的内容，显示字符级差异
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
        
        // 显示旧内容（删除部分）
        html.Append("<div class=\"diff-old-content\">");
        int oldPos = 0;
        
        foreach (var charDiff in line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Deleted))
        {
            // 添加未变化的部分
            if (oldPos < charDiff.Start)
            {
                html.Append(HttpUtility.HtmlEncode(oldContent.Substring(oldPos, charDiff.Start - oldPos)));
            }
            
            // 添加删除的部分
            html.Append($"<span class=\"{style.DeletedCharacterStyle}\">");
            html.Append(HttpUtility.HtmlEncode(charDiff.Content));
            html.Append("</span>");
            
            oldPos = charDiff.Start + charDiff.Length;
        }
        
        // 添加剩余未变化的部分
        if (oldPos < oldContent.Length)
        {
            html.Append(HttpUtility.HtmlEncode(oldContent.Substring(oldPos)));
        }
        
        html.AppendLine("</div>");
        
        // 显示新内容（新增部分）
        html.Append("<div class=\"diff-new-content\">");
        int newPos = 0;
        
        foreach (var charDiff in line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Added))
        {
            // 添加未变化的部分
            if (newPos < charDiff.Start)
            {
                html.Append(HttpUtility.HtmlEncode(newContent.Substring(newPos, charDiff.Start - newPos)));
            }
            
            // 添加新增的部分
            html.Append($"<span class=\"{style.AddedCharacterStyle}\">");
            html.Append(HttpUtility.HtmlEncode(charDiff.Content));
            html.Append("</span>");
            
            newPos = charDiff.Start + charDiff.Length;
        }
        
        // 添加剩余未变化的部分
        if (newPos < newContent.Length)
        {
            html.Append(HttpUtility.HtmlEncode(newContent.Substring(newPos)));
        }
        
        html.Append("</div>");
        
        return html.ToString();
    }
    
    /// <summary>
    /// 获取行样式类名
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
    /// 获取行类型符号
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
    /// 获取默认 CSS 样式
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