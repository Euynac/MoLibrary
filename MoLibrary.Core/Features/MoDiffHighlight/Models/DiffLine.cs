namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异行信息
/// </summary>
public class DiffLine
{
    /// <summary>
    /// 差异类型
    /// </summary>
    public EDiffLineType Type { get; set; }
    
    /// <summary>
    /// 原始行内容
    /// </summary>
    public string OldContent { get; set; } = string.Empty;
    
    /// <summary>
    /// 新行内容
    /// </summary>
    public string NewContent { get; set; } = string.Empty;
    
    /// <summary>
    /// 原始行号（从1开始，0表示不存在）
    /// </summary>
    public int OldLineNumber { get; set; }
    
    /// <summary>
    /// 新行号（从1开始，0表示不存在）
    /// </summary>
    public int NewLineNumber { get; set; }
    
    /// <summary>
    /// 字符级差异信息（如果启用字符级对比）
    /// </summary>
    public List<DiffCharacterRange>? CharacterDiffs { get; set; }
}

/// <summary>
/// 字符差异范围
/// </summary>
public class DiffCharacterRange
{
    /// <summary>
    /// 差异类型
    /// </summary>
    public EDiffLineType Type { get; set; }
    
    /// <summary>
    /// 起始位置
    /// </summary>
    public int Start { get; set; }
    
    /// <summary>
    /// 长度
    /// </summary>
    public int Length { get; set; }
    
    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}