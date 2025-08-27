namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异对比配置选项
/// </summary>
public class DiffHighlightOptions
{
    /// <summary>
    /// 对比模式
    /// </summary>
    public EDiffHighlightMode Mode { get; set; } = EDiffHighlightMode.Line;
    
    /// <summary>
    /// 输出格式
    /// </summary>
    public EDiffOutputFormat OutputFormat { get; set; } = EDiffOutputFormat.Html;
    
    /// <summary>
    /// 是否忽略空白字符
    /// </summary>
    public bool IgnoreWhitespace { get; set; } = false;
    
    /// <summary>
    /// 是否忽略大小写
    /// </summary>
    public bool IgnoreCase { get; set; } = false;
    
    /// <summary>
    /// 上下文行数（在变更前后显示多少行上下文）
    /// </summary>
    public int ContextLines { get; set; } = 3;
    
    /// <summary>
    /// 最大字符级差异长度（超过此长度将不进行字符级对比）
    /// </summary>
    public int MaxCharacterDiffLength { get; set; } = 1000;
    
    /// <summary>
    /// 自定义样式配置
    /// </summary>
    public DiffHighlightStyle? Style { get; set; }
}