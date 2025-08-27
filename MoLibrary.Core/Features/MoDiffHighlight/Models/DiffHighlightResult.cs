namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异对比高亮结果
/// </summary>
public class DiffHighlightResult
{
    /// <summary>
    /// 最终高亮内容（根据输出格式生成）
    /// </summary>
    public string HighlightedContent { get; set; } = string.Empty;
    
    /// <summary>
    /// 统计数据
    /// </summary>
    public DiffStatistics Statistics { get; set; } = new();
    
    /// <summary>
    /// 行级差异详情列表
    /// </summary>
    public List<DiffLine> Lines { get; set; } = new();
    
    /// <summary>
    /// 使用的配置选项
    /// </summary>
    public DiffHighlightOptions Options { get; set; } = new();
    
    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    public long ProcessingTimeMs { get; set; }
    
    /// <summary>
    /// 是否有任何变更
    /// </summary>
    public bool HasChanges => Statistics.TotalChanges > 0;
    
    /// <summary>
    /// 获取指定类型的行数
    /// </summary>
    /// <param name="type">差异类型</param>
    /// <returns>对应类型的行数</returns>
    public int GetLineCount(EDiffLineType type)
    {
        return Lines.Count(line => line.Type == type);
    }
}