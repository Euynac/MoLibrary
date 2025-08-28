namespace MoLibrary.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// 差异高亮样式配置
/// </summary>
public class DiffHighlightStyle
{
    /// <summary>
    /// 新增行样式类名或HTML属性
    /// </summary>
    public string AddedLineStyle { get; set; } = "diff-added";
    
    /// <summary>
    /// 删除行样式类名或HTML属性
    /// </summary>
    public string DeletedLineStyle { get; set; } = "diff-deleted";
    
    /// <summary>
    /// 修改行样式类名或HTML属性
    /// </summary>
    public string ModifiedLineStyle { get; set; } = "diff-modified";
    
    /// <summary>
    /// 未变化行样式类名或HTML属性
    /// </summary>
    public string UnchangedLineStyle { get; set; } = "diff-unchanged";
    
    /// <summary>
    /// 新增字符样式类名或HTML属性
    /// </summary>
    public string AddedCharacterStyle { get; set; } = "diff-added-char";
    
    /// <summary>
    /// 删除字符样式类名或HTML属性
    /// </summary>
    public string DeletedCharacterStyle { get; set; } = "diff-deleted-char";
    
    /// <summary>
    /// 行号样式类名或HTML属性
    /// </summary>
    public string LineNumberStyle { get; set; } = "diff-line-number";
    
    /// <summary>
    /// 容器样式类名或HTML属性
    /// </summary>
    public string ContainerStyle { get; set; } = "diff-container";
    
    /// <summary>
    /// 是否包含默认CSS样式
    /// </summary>
    public bool IncludeDefaultCss { get; set; } = true;
}