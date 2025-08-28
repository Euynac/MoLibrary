using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight.Algorithms;

/// <summary>
/// 差异算法接口
/// </summary>
public interface IDiffAlgorithm
{
    /// <summary>
    /// 计算两个文本的差异
    /// </summary>
    /// <param name="oldLines">原始文本行数组</param>
    /// <param name="newLines">新文本行数组</param>
    /// <param name="options">配置选项</param>
    /// <returns>差异行列表</returns>
    List<DiffLine> ComputeDiff(string[] oldLines, string[] newLines, DiffHighlightOptions options);
    
    /// <summary>
    /// 计算字符级差异
    /// </summary>
    /// <param name="oldText">原始文本</param>
    /// <param name="newText">新文本</param>
    /// <param name="options">配置选项</param>
    /// <returns>字符差异范围列表</returns>
    List<DiffCharacterRange> ComputeCharacterDiff(string oldText, string newText, DiffHighlightOptions options);
}