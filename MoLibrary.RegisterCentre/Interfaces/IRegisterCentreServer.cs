using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 注册中心服务端拥有的接口
/// </summary>
public interface IRegisterCentreServer : IRegisterCentreApiForClient
{
    /// <summary>
    /// 获取所有已注册微服务的状态
    /// </summary>
    /// <returns></returns>
    Task<Res<List<ServiceRegisterInfo>>> GetServicesStatus();
    /// <summary>
    /// 解除所有微服务注册
    /// </summary>
    /// <returns></returns>
    Task<Res> UnregisterAll();
    
    /// <summary>
    /// Get 方法批量执行调用所有已注册的服务
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> GetAsync<TResponse>(string callbackUrl);

    /// <summary>
    /// POST 方法批量执行调用所有已注册的服务
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> PostAsync<TRequest, TResponse>(string callbackUrl, TRequest req);
}