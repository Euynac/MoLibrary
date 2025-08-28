namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异统计信息
/// </summary>
public class DiffStatistics
{
    /// <summary>
    /// 总修改数（新增 + 删除 + 修改）
    /// </summary>
    public int TotalChanges { get; set; }
    
    /// <summary>
    /// 新增行数
    /// </summary>
    public int AddedLines { get; set; }
    
    /// <summary>
    /// 删除行数
    /// </summary>
    public int DeletedLines { get; set; }
    
    /// <summary>
    /// 修改行数
    /// </summary>
    public int ModifiedLines { get; set; }
    
    /// <summary>
    /// 未变化行数
    /// </summary>
    public int UnchangedLines { get; set; }
    
    /// <summary>
    /// 总行数（原文本）
    /// </summary>
    public int TotalOldLines { get; set; }
    
    /// <summary>
    /// 总行数（新文本）
    /// </summary>
    public int TotalNewLines { get; set; }
    
    /// <summary>
    /// 相似度百分比（0-100）
    /// </summary>
    public double SimilarityPercentage => TotalOldLines == 0 ? 
        (TotalNewLines == 0 ? 100.0 : 0.0) : 
        Math.Round((double)UnchangedLines / Math.Max(TotalOldLines, TotalNewLines) * 100, 2);
}