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
    /// 查询领导者状态
    /// </summary>
    Task<Res<LeaderStatusResponse>> GetLeaderStatus(LeaderStatusRequest req);
}