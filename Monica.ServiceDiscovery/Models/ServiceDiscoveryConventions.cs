namespace Monica.ServiceDiscovery.Models;

public static class ServiceDiscoveryConventions
{
    /// <summary>
    /// Route used to register a service instance with the registry.
    /// </summary>
    public const string RegistryRegister = "/registry/register";

    /// <summary>
    /// Route used to send a service-instance heartbeat.
    /// </summary>
    public const string RegistryHeartbeat = "/registry/heartbeat";

    /// <summary>
    /// Route used to query registry leader status.
    /// </summary>
    public const string RegistryLeaderStatus = "/registry/leader-status";

    /// <summary>
    /// Route used to query the status of registered services.
    /// </summary>
    public const string RegistryServiceStatus = "/registry/services";

    /// <summary>
    /// Route used to unregister every service instance owned by the caller.
    /// </summary>
    public const string RegistryUnregisterAll = "/registry/unregister-all";

    /// <summary>
    /// Route used to request that a client reconnect to its registry.
    /// </summary>
    public const string ClientReconnectRegistry = "/registry-client/reconnect";
}
