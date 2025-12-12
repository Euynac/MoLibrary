namespace MoLibrary.Core.Features.ObservableInstance;

/// <summary>
/// Provides observable tracking for any instance with state changes, exceptions, and history.
/// Replaces both ExceptionPool and HostedServiceObservableInfo internal state history functionality.
/// </summary>
public class ObservableAgent : IDisposable
{
    private readonly List<ObservableStateHistory> _stateHistory = [];
    private readonly ReaderWriterLockSlim _lock = new();

    // Identity
    /// <summary>
    /// Gets the unique instance identifier
    /// </summary>
    public string InstanceId { get; private set; }

    /// <summary>
    /// Gets the human-readable instance name
    /// </summary>
    public string InstanceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of the instance
    /// </summary>
    public Type? InstanceType { get; init; }

    /// <summary>
    /// Gets the instance key for keyed service instances (optional)
    /// </summary>
    public string? InstanceKey { get; init; }

    /// <summary>
    /// Gets the group ID for grouping related instances (optional)
    /// </summary>
    public string? GroupId { get; init; }

    // Configuration
    /// <summary>
    /// Gets the maximum number of history entries to retain
    /// </summary>
    public int MaxHistorySize { get; init; } = 100;

    // Current State
    /// <summary>
    /// Gets the current state of the instance
    /// </summary>
    public object? CurrentState { get; private set; }

    /// <summary>
    /// Gets the timestamp when the current state was entered
    /// </summary>
    public DateTime StateChangedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when this agent was created/registered
    /// </summary>
    public DateTime RegisteredAt { get; init; }

    // Statistics
    /// <summary>
    /// Gets the total number of state changes that have occurred
    /// </summary>
    public int TotalStateChanges { get; private set; }
    
    /// <summary>
    /// Gets the total number of exceptions that have occurred
    /// </summary>
    public int TotalExceptions { get; private set; }

    /// <summary>
    /// Gets the current number of history entries
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _stateHistory.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets the count of entries with exceptions
    /// </summary>
    public int ExceptionCount
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _stateHistory.Count(h => h.Exception != null);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets whether there are any exceptions in history
    /// </summary>
    public bool HasExceptions
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _stateHistory.Any(h => h.Exception != null);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the ObservableAgent class
    /// </summary>
    /// <param name="instanceId">Unique instance identifier</param>
    /// <param name="maxHistorySize">Maximum number of history entries to retain</param>
    public ObservableAgent(string instanceId, int maxHistorySize)
    {
        if (string.IsNullOrEmpty(instanceId))
            throw new ArgumentException("Instance ID cannot be empty", nameof(instanceId));
        if (maxHistorySize <= 0)
            throw new ArgumentException("Max history size must be greater than 0", nameof(maxHistorySize));

        InstanceId = instanceId;
        MaxHistorySize = maxHistorySize;
        RegisteredAt = DateTime.UtcNow;
        StateChangedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a state change with optional message and exception.
    /// This is the unified method that handles both state transitions and exception tracking.
    /// </summary>
    /// <param name="message">Descriptive message about the state change</param>
    /// <param name="newState">The new state to transition to (null if state not changing)</param>
    /// <param name="exception">Optional exception associated with this state change</param>
    public void RecordState(string message, object? newState = null, Exception? exception = null)
    {
        _lock.EnterWriteLock();
        try
        {
            var history = new ObservableStateHistory
            {
                PreviousState = CurrentState,
                CurrentState = newState,
                Message = message,
                Exception = exception,
                Timestamp = DateTime.UtcNow
            };

            _stateHistory.Add(history);
            if (_stateHistory.Count > MaxHistorySize)
            {
                _stateHistory.RemoveAt(0);
            }

            if(newState != null) 
            {
                CurrentState = newState;
            }
            StateChangedAt = DateTime.UtcNow;
            TotalStateChanges++;
            if (exception != null)
            {
                TotalExceptions++;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets all state history entries
    /// </summary>
    /// <returns>Read-only list of all history entries ordered by timestamp</returns>
    public IReadOnlyList<ObservableStateHistory> GetHistory()
    {
        _lock.EnterReadLock();
        try
        {
            return _stateHistory.ToList().AsReadOnly();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets recent history entries
    /// </summary>
    /// <param name="count">Number of recent entries to retrieve</param>
    /// <returns>Read-only list of recent history entries</returns>
    public IReadOnlyList<ObservableStateHistory> GetRecentHistory(int count)
    {
        if (count <= 0) return new List<ObservableStateHistory>().AsReadOnly();

        _lock.EnterReadLock();
        try
        {
            return _stateHistory
                .OrderByDescending(h => h.Timestamp)
                .Take(count)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all exceptions from history (convenience method for backward compatibility)
    /// </summary>
    /// <returns>Read-only list of history entries that have exceptions</returns>
    public IReadOnlyList<ObservableStateHistory> GetExceptions()
    {
        _lock.EnterReadLock();
        try
        {
            return _stateHistory
                .Where(h => h.Exception != null)
                .OrderByDescending(h => h.Timestamp)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets recent exceptions (convenience method for backward compatibility)
    /// </summary>
    /// <param name="count">Number of recent exceptions to retrieve</param>
    /// <returns>Read-only list of recent history entries with exceptions</returns>
    public IReadOnlyList<ObservableStateHistory> GetRecentExceptions(int count)
    {
        if (count <= 0) return new List<ObservableStateHistory>().AsReadOnly();

        _lock.EnterReadLock();
        try
        {
            return _stateHistory
                .Where(h => h.Exception != null)
                .OrderByDescending(h => h.Timestamp)
                .Take(count)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears all history
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _stateHistory.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Disposes resources used by this ObservableAgent
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
