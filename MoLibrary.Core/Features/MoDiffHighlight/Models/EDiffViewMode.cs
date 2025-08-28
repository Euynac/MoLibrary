namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异视图模式
/// </summary>
public enum EDiffViewMode
{
    /// <summary>
    /// 统一视图 - 单面板混合显示模式（类似GitHub Desktop）
    /// </summary>
    Unified,
    
    /// <summary>
    /// 分割视图 - 双面板对比显示模式（类似VS Code）
    /// </summary>
    Split
}