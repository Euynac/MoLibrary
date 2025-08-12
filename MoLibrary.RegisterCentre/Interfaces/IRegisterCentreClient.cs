using MoLibrary.RegisterCentre.Models;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 注册中心客户端侧接口
/// </summary>
public interface IRegisterCentreClient
{
    /// <summary>
    /// 获取当前微服务状态
    /// </summary>
    /// <returns></returns>
    public ServiceRegisterInfo GetServiceStatus();
}