using System.Text;
using Monica.Core.Features.MoDiffHighlight.Models;

namespace Monica.Core.Features.MoDiffHighlight.Renderers;

/// <summary>
/// Plain-text diff renderer.
/// </summary>
public class PlainTextDiffRenderer : IDiffHighlightRenderer
{
    public EDiffOutputFormat SupportedFormat => EDiffOutputFormat.PlainText;
    
    /// <summary>
    /// Renders diff lines as plain text.
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
    /// Renders a single diff line.
    /// </summary>
    public string RenderLine(DiffLine line, DiffHighlightStyle style)
    {
        var symbol = GetLineTypeSymbol(line.Type);
        var lineNumbers = GetLineNumbers(line);
        var content = GetLineContent(line);
        
        return $"{symbol} {lineNumbers} {content}";
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
    /// Gets the formatted line number pair for a diff line.
    /// </summary>
    private string GetLineNumbers(DiffLine line)
    {
        var oldNumber = line.OldLineNumber > 0 ? line.OldLineNumber.ToString() : "-";
        var newNumber = line.NewLineNumber > 0 ? line.NewLineNumber.ToString() : "-";
        
        return $"[{oldNumber},{newNumber}]";
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
            EDiffLineType.Modified => RenderModifiedContent(line),
            _ => line.NewContent ?? line.OldContent
        };
    }
    
    /// <summary>
    /// Renders a modified line with inline add and delete markers.
    /// </summary>
    private string RenderModifiedContent(DiffLine line)
    {
        if (line.CharacterDiffs == null || !line.CharacterDiffs.Any())
        {
            return $"{line.OldContent} -> {line.NewContent}";
        }
        
        var oldWithMarkers = new StringBuilder(line.OldContent);
        var newWithMarkers = new StringBuilder(line.NewContent);
        
        // Insert markers from the end so earlier indices stay valid.
        var deletedRanges = line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Deleted).ToList();
        foreach (var range in deletedRanges.OrderByDescending(r => r.Start))
        {
            oldWithMarkers.Insert(range.Start + range.Length, "]");
            oldWithMarkers.Insert(range.Start, "[-");
        }
        
        // Insert added markers in reverse order for the same reason.
        var addedRanges = line.CharacterDiffs.Where(d => d.Type == EDiffLineType.Added).ToList();
        foreach (var range in addedRanges.OrderByDescending(r => r.Start))
        {
            newWithMarkers.Insert(range.Start + range.Length, "]");
            newWithMarkers.Insert(range.Start, "[+");
        }
        
        return $"{oldWithMarkers} -> {newWithMarkers}";
    }
}
