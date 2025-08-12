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
    
    /// <summary>累计心跳次数</summary>
    public long HeartbeatCount { get; set; }
}