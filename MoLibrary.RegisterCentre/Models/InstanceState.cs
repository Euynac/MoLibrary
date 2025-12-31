using System.Text.Json.Serialization;
using MoLibrary.Core.GlobalJson.Converters;

namespace MoLibrary.RegisterCentre.Models;

/// <summary>
/// 实例注册状态
/// </summary>
public class InstanceState
{
    /// <summary>
    /// 服务名称（对应 AppId）
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// 实例 ID（对应 FromInstance）
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// 微服务显示名
    /// </summary>
    public required string AppName { get; set; }

    /// <summary>
    /// 项目名
    /// </summary>
    public required string ProjectName { get; set; }

    /// <summary>
    /// 子域名
    /// </summary>
    public string? DomainName { get; set; }

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
    /// 依赖子域列表
    /// </summary>
    public List<string>? DependentSubDomains { get; set; }

    /// <summary>
    /// 注册时间
    /// </summary>
    public DateTime RegistrationTime { get; set; }

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTime LastHeartbeatTime { get; set; }

    /// <summary>
    /// 服务实例元数据
    /// </summary>
    [JsonConverter(typeof(PreserveOriginalConverter<Dictionary<string, string>>))]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
