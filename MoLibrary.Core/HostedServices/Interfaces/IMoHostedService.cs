using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.Core.HostedServices.Models;

namespace MoLibrary.Core.HostedServices.Interfaces;

/// <summary>
/// Interface for observable hosted services with state management and exception tracking
/// </summary>
public interface IMoHostedService
{
    /// <summary>
    /// Gets the name of the service
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Gets a value indicating whether exception pool is enabled for this service
    /// </summary>
    bool EnableExceptionPool { get; }

    /// <summary>
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    int MaxHistorySize { get; }

    /// <summary>
    /// Gets the heartbeat interval for this service (null if not applicable or disabled)
    /// </summary>
    TimeSpan? HeartbeatInterval { get; }

    /// <summary>
    /// Gets the observable information for this service
    /// </summary>
    HostedServiceObservableInfo ObservableInfo { get; }

    /// <summary>
    /// Gets the exception pool for this service (null if disabled)
    /// </summary>
    ExceptionPool? ExceptionPool { get; }
}
