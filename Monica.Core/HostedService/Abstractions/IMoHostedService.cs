using Monica.Core.Execution;
using Monica.Core.HostedService.Models;

namespace Monica.Core.HostedService.Abstractions;

/// <summary>
/// Interface for observable hosted services with state management and exception tracking.
/// </summary>
public interface IMoHostedService : IExecutionAdapterOwnedComponent
{
    /// <summary>
    /// Gets the name of the service
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Gets the observable group identifier used to group related hosted services.
    /// A null value leaves the service ungrouped.
    /// </summary>
    string? ServiceGroupId { get; }

    /// <summary>
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    int MaxHistorySize { get; }

    /// <summary>
    /// Gets the heartbeat interval for this service (null if not applicable or disabled)
    /// </summary>
    TimeSpan? HeartbeatInterval { get; }

    /// <summary>
    /// Gets the runtime information for this service.
    /// </summary>
    HostedServiceRuntimeInfo RuntimeInfo { get; }
}
