using System.Text;
using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight.Renderers;

/// <summary>
/// 纯文本差异渲染器
/// </summary>
public class PlainTextDiffRenderer : IDiffHighlightRenderer
{
    public EDiffOutputFormat SupportedFormat => EDiffOutputFormat.PlainText;
    
    /// <summary>
    /// 渲染差异结果为纯文本格式
    /// </summary>
    public string Render(IEnumerable<DiffLine> lines, DiffHighlightStyle style)
    {
        var text = new StringBuilder();
        
        foreach (var line in lines)
        {
            text.AppendLine(RenderLine(line, style));
        }
        
        return text.ToString();
    }
    
    /// <summary>
    /// 渲染单行差异
    /// </summary>
    public string RenderLine(DiffLine line, DiffHighlightStyle style)
    {
        var symbol = GetLineTypeSymbol(line.Type);
        var lineNumbers = GetLineNumbers(line);
        var content = GetLineContent(line);
        
        return $"{symbol} {lineNumbers} {content}";
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
    /// 获取行号信息
    /// </summary>
    private string GetLineNumbers(DiffLine line)
    {
        var oldNumber = line.OldLineNumber > 0 ? line.OldLineNumber.ToString() : "-";
        var newNumber = line.NewLineNumber > 0 ? line.NewLineNumber.ToString() : "-";
        
        return $"[{oldNumber},{newNumber}]";
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
            EDiffLineType.Modified => RenderModifiedContent(line),
            _ => line.NewContent ?? line.OldContent
        };
    }
    
    /// <summary>
    /// 渲染修改行内容
    /// </summary>
    private string RenderModifiedContent(DiffLine line)
    {
        if (line.CharacterDiffs == null || !line.CharacterDiffs.Any())
        {
            return $"{line.OldContent} -> {line.NewContent}";
        }
        
        var oldWithMarkers = new StringBuilder(line.OldContent);
        var newWithMarkers = new StringBuilder(line.NewContent);
        
        // 在删除的字符前后添加标记
        var deletedRanges = line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Deleted).ToList();
        foreach (var range in deletedRanges.OrderByDescending(r => r.Start))
        {
            oldWithMarkers.Insert(range.Start + range.Length, "]");
            oldWithMarkers.Insert(range.Start, "[-");
        }
        
        // 在新增的字符前后添加标记
        var addedRanges = line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Added).ToList();
        foreach (var range in addedRanges.OrderByDescending(r => r.Start))
        {
            newWithMarkers.Insert(range.Start + range.Length, "]");
            newWithMarkers.Insert(range.Start, "[+");
        }
        
        return $"{oldWithMarkers} -> {newWithMarkers}";
    }
}