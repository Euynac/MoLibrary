namespace MoLibrary.RegisterCentre;

/// <summary>
/// 微服务注册状态
/// </summary>
public class RegisterServiceStatus
{
    /// <summary>
    /// 领域名
    /// </summary>
    public string? DomainName { get; set; }
    /// <summary>
    /// 微服务APPID
    /// </summary>
    public required string AppId { get; set; }
    /// <summary>
    /// 微服务显示名
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.Now;
    /// <summary>
    /// 微服务版本
    /// </summary>
    public long Version { get; set; }
    /// <summary>
    /// 微服务发布版本号
    /// </summary>
    public string? ReleaseVersion { get; set; }
    /// <summary>
    /// 项目名
    /// </summary>
    public required string ProjectName { get; set; }
    /// <summary>
    /// 来源IP端口等信息
    /// </summary>
    public string? FromClient { get; set; }
}