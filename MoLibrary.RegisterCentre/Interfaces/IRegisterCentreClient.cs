using MoLibrary.RegisterCentre.Models;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 注册中心客户端侧接口
/// </summary>
public interface IRegisterCentreClient
{
    /// <summary>
    /// 用于展示客户端监听地址元数据
    /// </summary>
    public static string ListeningAddressMetadataKey = "ListeningAddresses";
    /// <summary>
    /// 获取当前微服务状态
    /// </summary>
    /// <param name="isHeartbeatInfo"></param>
    /// <returns></returns>
    public ServiceRegisterInfo GetServiceStatus(bool isHeartbeatInfo = false);
}