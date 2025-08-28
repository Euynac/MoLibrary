using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight;

/// <summary>
/// 差异高亮渲染器接口
/// </summary>
public interface IDiffHighlightRenderer
{
    /// <summary>
    /// 支持的输出格式
    /// </summary>
    EDiffOutputFormat SupportedFormat { get; }
    
    /// <summary>
    /// 渲染差异结果为指定格式
    /// </summary>
    /// <param name="lines">差异行列表</param>
    /// <param name="style">样式配置</param>
    /// <returns>渲染后的内容</returns>
    string Render(IEnumerable<DiffLine> lines, DiffHighlightStyle style);
    
    /// <summary>
    /// 渲染单行差异
    /// </summary>
    /// <param name="line">差异行</param>
    /// <param name="style">样式配置</param>
    /// <returns>渲染后的行内容</returns>
    string RenderLine(DiffLine line, DiffHighlightStyle style);
}