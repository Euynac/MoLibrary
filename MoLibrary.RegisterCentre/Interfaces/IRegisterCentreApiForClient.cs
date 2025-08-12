using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 客户端调用的API接口
/// </summary>
public interface IRegisterCentreApiForClient
{
    /// <summary>
    /// 注册微服务
    /// </summary>
    Task<Res> Register(ServiceRegisterInfo req);

    /// <summary>
    /// 发送心跳
    /// </summary>
    Task<Res<ServiceHeartbeatResponse>> Heartbeat(ServiceHeartbeat req);
}