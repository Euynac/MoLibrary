namespace MoLibrary.Core.HostedServices.Models;

/// <summary>
/// Provides observable information about a hosted service including state, health, and history
/// </summary>
public class HostedServiceObservableInfo
{
    private readonly List<HostedServiceStateHistory> _stateHistory = new();
    private readonly object _stateLock = new();

    // Service Identity

    /// <summary>
    /// Gets the name of the service
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of the service
    /// </summary>
    public Type ServiceType { get; init; } = null!;

    /// <summary>
    /// Gets the service key for keyed service instances (optional)
    /// </summary>
    public string? ServiceKey { get; init; }

    // Current State

    /// <summary>
    /// Gets or sets the current state of the service
    /// </summary>
    public HostedServiceState CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the current state was entered
    /// </summary>
    public DateTime StateChangedAt { get; set; }

    /// <summary>
    /// Gets the timestamp when the service was registered
    /// </summary>
    public DateTime RegisteredAt { get; init; }

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

    // Exception Pool (if enabled)

    /// <summary>
    /// Gets the exception pool ID associated with this service (if exception pool is enabled)
    /// </summary>
    public string? ExceptionPoolId { get; set; }

    /// <summary>
    /// Gets a value indicating whether this service has an exception pool
    /// </summary>
    public bool HasExceptionPool => !string.IsNullOrEmpty(ExceptionPoolId);

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

    // State History

    /// <summary>
    /// Gets the state history for this service
    /// </summary>
    public IReadOnlyList<HostedServiceStateHistory> StateHistory
    {
        get
        {
            lock (_stateLock)
            {
                return _stateHistory.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the maximum number of history entries to retain
    /// </summary>
    public int MaxHistorySize { get; init; } = 100;

    // Execution Statistics

    /// <summary>
    /// Gets the total number of state changes that have occurred
    /// </summary>
    public long TotalStateChanges { get; private set; }

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
        lock (_stateLock)
        {
            var history = new HostedServiceStateHistory
            {
                PreviousState = CurrentState,
                CurrentState = newState,
                Message = message,
                Exception = exception
            };

            _stateHistory.Add(history);
            if (_stateHistory.Count > MaxHistorySize)
            {
                _stateHistory.RemoveAt(0);
            }

            CurrentState = newState;
            StateChangedAt = DateTime.UtcNow;
            TotalStateChanges++;
        }
    }
}
