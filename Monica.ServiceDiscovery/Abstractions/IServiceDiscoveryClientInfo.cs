using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Abstractions;

/// <summary>
/// Registration center client information interface
/// </summary>
public interface IServiceDiscoveryClientInfo
{
    /// <summary>
    /// Used to display client listening address metadata
    /// </summary>
    const string LISTENING_ADDRESS_METADATA_KEY = "LISTENING_ADDRESS";

    /// <summary>
    /// Get complete status information of the current microservice instance
    /// </summary>
    /// <param name="isHeartbeatInfo">Whether it is heartbeat information (heartbeat does not include environment variables and listening address metadata)</param>
    /// <returns>Instance status information</returns>
    InstanceState GetServiceStatus(bool isHeartbeatInfo = true);

    /// <summary>
    /// Get the registration time of the current instance (recorded when first registered, null means it has not been registered yet)
    /// </summary>
    DateTime? RegistrationTime { get; }

    /// <summary>
    /// Get the last heartbeat time of the current instance (updated with each heartbeat, null means the heartbeat has not been sent yet)
    /// </summary>
    DateTime? LastHeartbeatTime { get; }

    /// <summary>
    /// Set the registration time (only called when the first registration is successful)
    /// </summary>
    /// <param name="time">Registration time</param>
    void SetRegistrationTime(DateTime time);

    /// <summary>
    /// Update the last heartbeat time (called after each heartbeat is successful)
    /// </summary>
    /// <param name="time">heartbeat time</param>
    void UpdateLastHeartbeatTime(DateTime time);
}