namespace MoLibrary.RegisterCentre.Models;

public class ServiceInstance
{
    /// <summary>实例标识（FromClient）</summary>
    public required string InstanceId { get; set; }

    /// <summary>实例注册信息</summary>
    public required ServiceRegisterInfo RegisterInfo { get; set; }

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
}