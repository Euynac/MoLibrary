using MoLibrary.RegisterCentre.Models;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 注册中心客户端信息接口
/// </summary>
public interface IRegisterCentreClientInfo
{
    /// <summary>
    /// 用于展示客户端监听地址元数据
    /// </summary>
    const string LISTENING_ADDRESS_METADATA_KEY = "LISTENING_ADDRESS";

    /// <summary>
    /// 获取当前微服务实例的完整状态信息
    /// </summary>
    /// <param name="isHeartbeatInfo">是否为心跳信息（心跳时不包含环境变量和监听地址元数据）</param>
    /// <returns>实例状态信息</returns>
    InstanceState GetServiceStatus(bool isHeartbeatInfo = false);
}