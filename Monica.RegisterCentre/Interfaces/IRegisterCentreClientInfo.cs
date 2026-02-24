using Monica.RegisterCentre.Models;

namespace Monica.RegisterCentre.Interfaces;

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

    /// <summary>
    /// 获取当前实例的注册时间（首次注册时记录，null 表示尚未注册）
    /// </summary>
    DateTime? RegistrationTime { get; }

    /// <summary>
    /// 获取当前实例的最后心跳时间（每次心跳更新，null 表示尚未发送心跳）
    /// </summary>
    DateTime? LastHeartbeatTime { get; }

    /// <summary>
    /// 设置注册时间（仅在首次注册成功时调用）
    /// </summary>
    /// <param name="time">注册时间</param>
    void SetRegistrationTime(DateTime time);

    /// <summary>
    /// 更新最后心跳时间（每次心跳成功后调用）
    /// </summary>
    /// <param name="time">心跳时间</param>
    void UpdateLastHeartbeatTime(DateTime time);
}