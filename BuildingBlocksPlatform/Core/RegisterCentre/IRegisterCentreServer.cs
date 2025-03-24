using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.Core.RegisterCentre;

/// <summary>
/// 注册中心服务端拥有的接口
/// </summary>
public interface IRegisterCentreServer : IRegisterCentreApiForClient
{
    /// <summary>
    /// 获取所有已注册微服务的状态
    /// </summary>
    /// <returns></returns>
    Task<Res<List<RegisterServiceStatus>>> GetServicesStatus();
    /// <summary>
    /// 解除所有微服务注册
    /// </summary>
    /// <returns></returns>
    Task<Res> UnregisterAll();
    
    /// <summary>
    /// Get 方法批量执行调用所有已注册的服务
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<RegisterServiceStatus, Res<TResponse>>> GetAsync<TResponse>(string callbackUrl);

    /// <summary>
    /// POST 方法批量执行调用所有已注册的服务
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<RegisterServiceStatus, Res<TResponse>>> PostAsync<TRequest, TResponse>(string callbackUrl, TRequest req);
}
/// <summary>
/// 注册中心客户端侧接口
/// </summary>
public interface IRegisterCentreClient
{
    /// <summary>
    /// 获取当前微服务状态
    /// </summary>
    /// <returns></returns>
    public RegisterServiceStatus GetServiceStatus();
}

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

/// <summary>
/// 注册中心客户端侧连接逻辑
/// </summary>
public interface IRegisterCentreServerConnector : IRegisterCentreApiForClient
{
    /// <summary>
    /// 客户端开始注册微服务
    /// </summary>
    /// <returns></returns>
    Task DoingRegister();
}