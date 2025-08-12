using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 注册中心服务端提供给客户端连接接口
/// </summary>
public interface IRegisterCentreApiForClient
{
    /// <summary>
    /// 注册微服务
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    Task<Res> Register(RegisterServiceStatus req);

    /// <summary>
    /// 心跳
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    Task<Res> Heartbeat(RegisterServiceStatus req);
}