using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.Core.HostedServices.Models;

namespace MoLibrary.Core.HostedServices;

/// <summary>
/// Base class for observable IHostedService implementations with built-in state management and exception tracking
/// </summary>
public abstract class MoHostedService(
    IExceptionPoolManager? exceptionPoolManager = null,
    ILogger? logger = null) : IHostedService
{
    protected readonly ILogger Logger = logger ?? NullLogger.Instance;

    // Observable state - protected so derived classes can access, managed by base class
    protected HostedServiceObservableInfo ObservableInfo { get; private set; } = null!;
    internal ExceptionPool? ExceptionPool { get; private set; }

    private readonly List<HostedServiceStateHistory> _stateHistory = new();
    private readonly object _stateLock = new();

    // Abstract/Virtual members

    /// <summary>
    /// Gets the name of the service for identification purposes
    /// </summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    /// Gets a value indicating whether exception pool is enabled for this service
    /// </summary>
    protected virtual bool EnableExceptionPool => true;

    /// <summary>
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    protected virtual int MaxHistorySize => 100;

    /// <summary>
    /// Initializes observable info (called by registration system)
    /// </summary>
    /// <param name="info">The observable info to initialize</param>
    internal void InitializeObservableInfo(HostedServiceObservableInfo info)
    {
        ObservableInfo = info;

        if (EnableExceptionPool && exceptionPoolManager != null)
        {
            var poolId = $"HostedService_{ServiceName}_{Guid.NewGuid():N}";
            ExceptionPool = exceptionPoolManager.Create(poolId, opt =>
            {
                opt.MaxSize = MaxHistorySize;
                opt.EnableEventTrigger = true;
            });
            ObservableInfo.ExceptionPoolId = poolId;
        }
    }

    /// <summary>
    /// Records a state change with optional message and exception
    /// </summary>
    /// <param name="newState">The new state to transition to</param>
    /// <param name="message">Descriptive message about the state change</param>
    /// <param name="exception">Optional exception associated with this state change</param>
    protected void RecordStateChange(
        HostedServiceState newState,
        string message,
        Exception? exception = null)
    {
        lock (_stateLock)
        {
            var history = new HostedServiceStateHistory
            {
                PreviousState = ObservableInfo.CurrentState,
                CurrentState = newState,
                Message = message,
                Exception = exception
            };

            _stateHistory.Add(history);
            if (_stateHistory.Count > MaxHistorySize)
            {
                _stateHistory.RemoveAt(0);
            }

            ObservableInfo.CurrentState = newState;
            ObservableInfo.StateChangedAt = DateTime.UtcNow;
            ObservableInfo.TotalStateChanges++;
            ObservableInfo.StateHistory = _stateHistory.AsReadOnly();

            // Add to exception pool if error and pool is enabled
            if (exception != null && ExceptionPool != null)
            {
                ExceptionPool.AddException(exception, this, message);
            }
        }
    }

    /// <summary>
    /// Starts the hosted service
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordStateChange(HostedServiceState.Starting, "Service starting");
            ObservableInfo.StartedAt = DateTime.UtcNow;

            await OnStartingAsync(cancellationToken);

            RecordStateChange(HostedServiceState.Running, "Service started successfully");

            await OnStartedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            RecordStateChange(HostedServiceState.Faulted, "Service start failed", ex);
            Logger.LogError(ex, "{ServiceName} failed to start", ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Stops the hosted service
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordStateChange(HostedServiceState.Stopping, "Service stopping");

            await OnStoppingAsync(cancellationToken);

            ObservableInfo.StoppedAt = DateTime.UtcNow;
            RecordStateChange(HostedServiceState.Stopped, "Service stopped successfully");

            await OnStoppedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            RecordStateChange(HostedServiceState.Faulted, "Service stop failed", ex);
            Logger.LogError(ex, "{ServiceName} failed to stop gracefully", ServiceName);
            throw;
        }
    }

    // Template method hooks

    /// <summary>
    /// Called during service startup, before the service is marked as running
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual Task OnStartingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called after the service has started successfully
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual Task OnStartedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called during service shutdown, before the service is marked as stopped
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual Task OnStoppingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called after the service has stopped successfully
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual Task OnStoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
