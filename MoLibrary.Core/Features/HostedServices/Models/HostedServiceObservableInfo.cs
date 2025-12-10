using MoLibrary.Core.Features.ObservableInstance;

namespace MoLibrary.Core.Features.HostedServices.Models;

/// <summary>
/// Provides observable information about a hosted service including state, health, and history.
/// Now uses ObservableAgent for core tracking functionality.
/// </summary>
public class HostedServiceObservableInfo
{
    private readonly ObservableAgent _agent;

    /// <summary>
    /// Initializes a new instance of HostedServiceObservableInfo with an ObservableAgent
    /// </summary>
    /// <param name="agent">The observable agent that tracks state and history</param>
    public HostedServiceObservableInfo(ObservableAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    // Service Identity (delegates to agent)

    /// <summary>
    /// Gets the name of the service
    /// </summary>
    public string ServiceName => _agent.InstanceName;

    /// <summary>
    /// Gets the type of the service
    /// </summary>
    public Type ServiceType => _agent.InstanceType ?? typeof(object);

    /// <summary>
    /// Gets the service key for keyed service instances (optional)
    /// </summary>
    public string? ServiceKey => _agent.InstanceKey;

    // Current State (delegates to agent with typed state)

    /// <summary>
    /// Gets the current state of the service
    /// </summary>
    public HostedServiceState CurrentState =>
        _agent.CurrentState is HostedServiceState state ? state : HostedServiceState.NotStarted;

    /// <summary>
    /// Gets the timestamp when the current state was entered
    /// </summary>
    public DateTime StateChangedAt => _agent.StateChangedAt;

    /// <summary>
    /// Gets the timestamp when the service was registered
    /// </summary>
    public DateTime RegisteredAt => _agent.RegisteredAt;

    /// <summary>
    /// Gets or sets the timestamp when the service was started (StartAsync called)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the service was stopped (StopAsync completed)
    /// </summary>
    public DateTime? StoppedAt { get; set; }

    // Health Information

    /// <summary>
    /// Gets a value indicating whether the service is healthy (Running or Executing state)
    /// </summary>
    public bool IsHealthy => CurrentState is HostedServiceState.Running or HostedServiceState.Executing;

    /// <summary>
    /// Gets a value indicating whether the service is in a degraded state
    /// </summary>
    public bool IsDegraded => CurrentState == HostedServiceState.Degraded;

    /// <summary>
    /// Gets a value indicating whether the service is in a faulted state
    /// </summary>
    public bool IsFaulted => CurrentState == HostedServiceState.Faulted;


    // Heartbeat Information (BackgroundService only)

    /// <summary>
    /// Gets or sets the timestamp of the last heartbeat
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// Gets the configured heartbeat interval (null if heartbeat is disabled)
    /// </summary>
    public TimeSpan? HeartbeatInterval { get; init; }

    /// <summary>
    /// Gets a value indicating whether heartbeat monitoring is enabled
    /// </summary>
    public bool IsHeartbeatEnabled => HeartbeatInterval.HasValue;

    /// <summary>
    /// Gets a value indicating whether the heartbeat is healthy
    /// (either heartbeat is disabled or last heartbeat is within acceptable threshold)
    /// </summary>
    public bool IsHeartbeatHealthy =>
        !IsHeartbeatEnabled ||
        (LastHeartbeat.HasValue &&
         (DateTime.UtcNow - LastHeartbeat.Value) < (HeartbeatInterval!.Value * 2));

    // State History (delegates to agent with typed conversion)

    /// <summary>
    /// Gets the state history for this service
    /// </summary>
    public IReadOnlyList<HostedServiceStateHistory> StateHistory =>
        _agent.GetHistory()
            .Select(h => new HostedServiceStateHistory
            {
                Timestamp = h.Timestamp,
                PreviousState = h.PreviousState as HostedServiceState?,
                CurrentState = h.CurrentState is HostedServiceState s ? s : HostedServiceState.NotStarted,
                Message = h.Message,
                Exception = h.Exception
            })
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Gets the maximum number of history entries to retain
    /// </summary>
    public int MaxHistorySize => _agent.MaxHistorySize;

    // Execution Statistics

    /// <summary>
    /// Gets the total number of state changes that have occurred
    /// </summary>
    public long TotalStateChanges => _agent.TotalStateChanges;

    /// <summary>
    /// Gets the uptime of the service (time since started, null if not started or already stopped)
    /// </summary>
    public TimeSpan? Uptime => StartedAt.HasValue && StoppedAt == null
        ? DateTime.UtcNow - StartedAt.Value
        : null;

    /// <summary>
    /// Records a state change and adds it to the history
    /// </summary>
    /// <param name="newState">The new state to transition to</param>
    /// <param name="message">Descriptive message about the state change</param>
    /// <param name="exception">Optional exception associated with this state change</param>
    public void RecordStateChange(HostedServiceState newState, string message, Exception? exception = null)
    {
        _agent.RecordStateChange(newState, message, exception);
    }
}
