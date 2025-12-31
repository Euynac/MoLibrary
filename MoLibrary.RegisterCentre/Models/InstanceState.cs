namespace MoLibrary.RegisterCentre.Models;

/// <summary>
/// 实例注册状态
/// </summary>
public class InstanceState
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// 注册时间
    /// </summary>
    public DateTime RegistrationTime { get; set; }

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTime LastHeartbeatTime { get; set; }

    /// <summary>
    /// 服务注册信息
    /// </summary>
    public ServiceRegisterInfo? RegisterInfo { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
