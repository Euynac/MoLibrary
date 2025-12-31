namespace MoLibrary.RegisterCentre.Models;

public class ServiceInstance
{
    /// <summary>实例标识（FromClient）</summary>
    public required string InstanceId { get; set; }

    /// <summary>服务名称（对应 AppId）</summary>
    public required string ServiceName { get; set; }

    /// <summary>微服务显示名</summary>
    public required string AppName { get; set; }

    /// <summary>项目名</summary>
    public required string ProjectName { get; set; }

    /// <summary>子域名</summary>
    public string? DomainName { get; set; }

    /// <summary>微服务构建时间</summary>
    public DateTime BuildTime { get; set; }

    /// <summary>微服务程序集版本号</summary>
    public string? AssemblyVersion { get; set; }

    /// <summary>微服务发布版本号</summary>
    public string? ReleaseVersion { get; set; }

    /// <summary>依赖子域列表</summary>
    public List<string>? DependentSubDomains { get; set; }

    /// <summary>服务实例元数据</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>实例状态</summary>
    public ServiceStatus Status { get; set; }

    /// <summary>最后心跳时间</summary>
    public DateTime LastHeartbeatTime { get; set; }

    /// <summary>注册时间</summary>
    public DateTime RegistrationTime { get; set; }

    /// <summary>
    /// 是否为领导者
    /// <para>领导者选举规则：在同一AppId的所有Running状态实例中，注册时间最早的实例被选为领导者</para>
    /// <para>当领导者离线时，会自动从剩余的Running实例中重新选举</para>
    /// </summary>
    public bool IsLeader { get; set; }

    /// <summary>
    /// 从 InstanceState 创建 ServiceInstance
    /// </summary>
    public static ServiceInstance FromInstanceState(
        InstanceState state,
        ServiceStatus status,
        bool isLeader)
    {
        return new ServiceInstance
        {
            InstanceId = state.InstanceId,
            ServiceName = state.ServiceName,
            AppName = state.AppName,
            ProjectName = state.ProjectName,
            DomainName = state.DomainName,
            BuildTime = state.BuildTime,
            AssemblyVersion = state.AssemblyVersion,
            ReleaseVersion = state.ReleaseVersion,
            DependentSubDomains = state.DependentSubDomains,
            Metadata = state.Metadata,
            Status = status,
            LastHeartbeatTime = state.LastHeartbeatTime,
            RegistrationTime = state.RegistrationTime,
            IsLeader = isLeader
        };
    }
}