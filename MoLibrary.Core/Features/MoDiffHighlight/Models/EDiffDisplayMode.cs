namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 统一视图显示模式
/// </summary>
public enum EDiffDisplayMode
{
    /// <summary>
    /// 比较模式 - 显示完整的差异对比
    /// </summary>
    Compare,
    
    /// <summary>
    /// 新值模式 - 只显示新内容
    /// </summary>
    NewOnly,
    
    /// <summary>
    /// 旧值模式 - 只显示旧内容
    /// </summary>
    OldOnly
}