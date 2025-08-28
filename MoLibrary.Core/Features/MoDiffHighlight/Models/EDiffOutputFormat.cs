namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异输出格式
/// </summary>
public enum EDiffOutputFormat
{
    /// <summary>
    /// HTML格式（带样式，适合Web展示）
    /// </summary>
    Html,
    
    /// <summary>
    /// Markdown格式（适合文档）
    /// </summary>
    Markdown,
    
    /// <summary>
    /// 纯文本格式（带标记）
    /// </summary>
    PlainText
}