namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异对比模式
/// </summary>
public enum EDiffHighlightMode
{
    /// <summary>
    /// 行级对比（默认）
    /// </summary>
    Line,
    
    /// <summary>
    /// 字符级对比
    /// </summary>
    Character,
    
    /// <summary>
    /// 混合模式（智能）
    /// </summary>
    Mixed
}