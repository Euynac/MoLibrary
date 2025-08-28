using System.Text;
using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight.Renderers;

/// <summary>
/// Markdown 差异渲染器
/// </summary>
public class MarkdownDiffRenderer : IDiffHighlightRenderer
{
    public EDiffOutputFormat SupportedFormat => EDiffOutputFormat.Markdown;
    
    /// <summary>
    /// 渲染差异结果为 Markdown 格式
    /// </summary>
    public string Render(IEnumerable<DiffLine> lines, DiffHighlightStyle style)
    {
        var markdown = new StringBuilder();
        
        // 添加代码块标记
        markdown.AppendLine("```diff");
        
        foreach (var line in lines)
        {
            markdown.AppendLine(RenderLine(line, style));
        }
        
        markdown.AppendLine("```");
        
        return markdown.ToString();
    }
    
    /// <summary>
    /// 渲染单行差异
    /// </summary>
    public string RenderLine(DiffLine line, DiffHighlightStyle style)
    {
        var symbol = GetLineTypeSymbol(line.Type);
        var content = GetLineContent(line);
        
        return $"{symbol} {content}";
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
    /// 获取行内容
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