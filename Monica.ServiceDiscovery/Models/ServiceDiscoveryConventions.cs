namespace Monica.ServiceDiscovery.Models;

public static class ServiceDiscoveryConventions
{
    /// <summary>
    /// Microservice registration registration center
    /// </summary>
    public static string RegistryRegister = "/registry/register";

    /// <summary>
    /// Microservice heartbeat
    /// </summary>
    public static string RegistryHeartbeat = "/registry/heartbeat";

    /// <summary>
    /// Query leader status
    /// </summary>
    public static string RegistryLeaderStatus = "/registry/leader-status";

    /// <summary>
    /// Get the status of all microservices
    /// </summary>
    public static string RegistryServiceStatus = "/registry/services";

    /// <summary>
    /// Cancel all microservice registrations
    /// </summary>
    public static string RegistryUnregisterAll = "/registry/unregister-all";

    /// <summary>
    /// Test reconnection registry service
    /// </summary>
    public static string ClientReconnectRegistry = "/registry-client/reconnect";
}
