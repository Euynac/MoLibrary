namespace MoLibrary.RegisterCentre.Models;

public class ServiceHeartbeat
{
    /// <summary>微服务APPID</summary>
    public required string AppId { get; set; }
    
    /// <summary>微服务构建时间</summary>
    public DateTime BuildTime { get; set; }
    
    /// <summary>微服务程序集版本号</summary>
    public string? AssemblyVersion { get; set; }
    
    /// <summary>微服务发布版本号</summary>
    public string? ReleaseVersion { get; set; }
    
    /// <summary>来源IP端口等信息（用于识别实例）</summary>
    public string? FromClient { get; set; }
}