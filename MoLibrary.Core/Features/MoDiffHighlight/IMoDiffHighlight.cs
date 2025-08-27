using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight;

/// <summary>
/// 文本差异对比高亮接口
/// </summary>
public interface IMoDiffHighlight
{
    /// <summary>
    /// 执行文本对比并生成高亮结果
    /// </summary>
    /// <param name="oldText">原始文本</param>
    /// <param name="newText">新文本</param>
    /// <param name="options">配置选项</param>
    /// <returns>差异对比高亮结果</returns>
    Task<DiffHighlightResult> HighlightAsync(string oldText, string newText, DiffHighlightOptions? options = null);
    
    /// <summary>
    /// 执行文本对比并生成高亮结果（同步版本）
    /// </summary>
    /// <param name="oldText">原始文本</param>
    /// <param name="newText">新文本</param>
    /// <param name="options">配置选项</param>
    /// <returns>差异对比高亮结果</returns>
    DiffHighlightResult Highlight(string oldText, string newText, DiffHighlightOptions? options = null);
    
    /// <summary>
    /// 设置自定义渲染器
    /// </summary>
    /// <param name="renderer">渲染器实例</param>
    void SetRenderer(IDiffHighlightRenderer renderer);
}