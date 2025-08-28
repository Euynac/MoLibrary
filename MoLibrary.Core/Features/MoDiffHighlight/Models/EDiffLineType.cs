namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异行类型
/// </summary>
public enum EDiffLineType
{
    /// <summary>
    /// 未变化
    /// </summary>
    Unchanged,
    
    /// <summary>
    /// 新增
    /// </summary>
    Added,
    
    /// <summary>
    /// 删除
    /// </summary>
    Deleted,
    
    /// <summary>
    /// 修改
    /// </summary>
    Modified
}