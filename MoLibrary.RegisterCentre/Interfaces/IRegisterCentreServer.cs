using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 服务端完整接口
/// </summary>
public interface IRegisterCentreServer : IRegisterCentreApiForClient
{
    /// <summary>
    /// 获取所有已注册微服务的状态
    /// </summary>
    Task<Res<List<RegisteredServiceStatus>>> GetServicesStatus();
    
    /// <summary>
    /// 解除所有微服务注册
    /// </summary>
    Task<Res> UnregisterAll();
    
    /// <summary>
    /// Get 方法批量执行调用所有已注册的服务
    /// </summary>
    Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> GetAsync<TResponse>(string callbackUrl);

    /// <summary>
    /// POST 方法批量执行调用所有已注册的服务
    /// </summary>
    Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> PostAsync<TRequest, TResponse>(string callbackUrl, TRequest req);
}