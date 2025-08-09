namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 项目单元告警信息
/// </summary>
public class ProjectUnitAlert
{
    /// <summary>
    /// 告警级别
    /// </summary>
    public EAlertLevel Level { get; set; }
    
    /// <summary>
    /// 告警内容
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// 告警来源
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 告警级别枚举
/// </summary>
public enum EAlertLevel
{
    /// <summary>
    /// 信息级别
    /// </summary>
    Info,
    
    /// <summary>
    /// 警告级别
    /// </summary>
    Warning,
    
    /// <summary>
    /// 错误级别
    /// </summary>
    Error
}