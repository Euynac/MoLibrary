using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 用于客户端调用的服务端侧API接口
/// </summary>
public interface IRegisterCentreServerApiForClient
{
    /// <summary>
    /// 接收客户端注册请求
    /// </summary>
    Task<Res> Register(ServiceRegisterInfo req);

    /// <summary>
    /// 接收客户端心跳
    /// </summary>
    Task<Res<ServiceHeartbeatResponse>> Heartbeat(ServiceHeartbeat req);
    /// <summary>
    /// 查询指定实例在服务集群中的领导者状态（Leader/Follower/Looking）
    /// </summary>
    Task<Res<LeaderStatusResponse>> GetLeaderStatus(LeaderStatusRequest req);
}