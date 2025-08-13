namespace MoLibrary.RegisterCentre.Models;

/// <summary>
/// 微服务注册信息
/// </summary>
public class ServiceRegisterInfo
{
    /// <summary>
    /// 子域名
    /// </summary>
    public string? DomainName { get; set; }
    /// <summary>
    /// 微服务APPID
    /// </summary>
    public required string AppId { get; set; }
    /// <summary>
    /// 微服务显示名
    /// </summary>
    public required string AppName { get; set; }
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.Now;
    /// <summary>
    /// 微服务构建时间
    /// </summary>
    public DateTime BuildTime { get; set; }
    /// <summary>
    /// 微服务程序集版本号
    /// </summary>
    public string? AssemblyVersion { get; set; }
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