namespace MoLibrary.RegisterCentre.Interfaces;

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