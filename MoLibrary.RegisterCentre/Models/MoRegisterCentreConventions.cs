namespace MoLibrary.RegisterCentre.Models;

public static class MoRegisterCentreConventions
{
    /// <summary>
    /// 微服务注册注册中心
    /// </summary>
    public static string ServerCentreRegister = "/centre-server/register";
    
    /// <summary>
    /// 微服务心跳
    /// </summary>
    public static string ServerCentreHeartbeat = "/centre-server/heartbeat";
    
    /// <summary>
    /// 获取所有微服务状态
    /// </summary>
    public static string ServerCentreGetServicesStatus = "/centre-server/services";
    
    /// <summary>
    /// 取消所有微服务注册
    /// </summary>
    public static string ServerCentreUnregisterAll = "/centre-server/unregister-all";
  
    /// <summary>
    /// 测试重连注册中心
    /// </summary>
    public static string ClientReconnectCentre = "/centre-client/reconnect";
}