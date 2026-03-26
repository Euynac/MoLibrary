namespace Monica.ServiceDiscovery.Models;

public static class ServiceDiscoveryConventions
{
    /// <summary>
    /// 微服务注册注册中心
    /// </summary>
    public static string RegistryRegister = "/registry/register";

    /// <summary>
    /// 微服务心跳
    /// </summary>
    public static string RegistryHeartbeat = "/registry/heartbeat";

    /// <summary>
    /// 查询领导者状态
    /// </summary>
    public static string RegistryLeaderStatus = "/registry/leader-status";

    /// <summary>
    /// 获取所有微服务状态
    /// </summary>
    public static string RegistryServiceStatus = "/registry/services";

    /// <summary>
    /// 取消所有微服务注册
    /// </summary>
    public static string RegistryUnregisterAll = "/registry/unregister-all";

    /// <summary>
    /// 测试重连注册表服务
    /// </summary>
    public static string ClientReconnectRegistry = "/registry-client/reconnect";
}
