using Monica.Core.Features.HostedServices.Models;

namespace Monica.Core.Features.HostedServices.Interfaces;

/// <summary>
/// Interface for observable hosted services with state management and exception tracking.
/// Now uses ObservableAgent for unified tracking.
/// </summary>
public interface IMoHostedService
{
    /// <summary>
    /// Gets the name of the service
    /// </summary>
    string ServiceName { get; }

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
}
