namespace Monica.Core.Features.HostedServices.Models;

/// <summary>
/// Represents the current state of a hosted service
/// </summary>
public enum HostedServiceState
{
    /// <summary>
    /// Service has been registered but not yet started
    /// </summary>
    NotStarted,

    /// <summary>
    /// Service is in the process of starting (StartAsync executing)
    /// </summary>
    Starting,

    /// <summary>
    /// Service has started successfully and is running
    /// </summary>
    Running,

    /// <summary>
    /// Service is actively executing background work
    /// </summary>
    Executing,

    /// <summary>
    /// Service is in the process of stopping gracefully
    /// </summary>
    Stopping,

    /// <summary>
    /// Service has stopped successfully
    /// </summary>
    Stopped,

    /// <summary>
    /// Service encountered a critical error and is in a faulted state
    /// </summary>
    Faulted,

    /// <summary>
    /// Service is in a degraded state (has warnings but still functional)
    /// </summary>
    Degraded
}
