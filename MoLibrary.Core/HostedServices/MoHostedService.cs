using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Core.HostedServices.Interfaces;
using MoLibrary.Core.HostedServices.Models;
using MoLibrary.Core.ObservableInstance;

namespace MoLibrary.Core.HostedServices;

/// <summary>
/// Base class for observable IHostedService implementations with built-in state management and exception tracking.
/// Now uses ObservableAgent for unified tracking.
/// </summary>
public abstract class MoHostedService(
    IObservableInstanceManager observableManager,
    ILogger? logger = null) : IHostedService, IMoHostedService
{
    protected readonly ILogger Logger = logger ?? NullLogger.Instance;

    // IMoHostedService implementation

    /// <summary>
    /// Gets the name of the service for identification purposes
    /// </summary>
    public abstract string ServiceName { get; }

    /// <summary>
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    public virtual int MaxHistorySize => 100;

    /// <summary>
    /// Gets the heartbeat interval (always null for MoHostedService, only applicable to MoBackgroundService)
    /// </summary>
    public virtual TimeSpan? HeartbeatInterval => null;

    /// <summary>
    /// Gets the observable information for this service
    /// </summary>
    public HostedServiceObservableInfo ObservableInfo { get; private set; } = null!;


    /// <summary>
    /// Initializes observable info (called by manager during registration)
    /// </summary>
    internal void InitializeObservableInfo()
    {
        var agentId = $"HostedService_{ServiceName}_{Guid.NewGuid():N}";
        ObservableInfo = new HostedServiceObservableInfo(observableManager.Create(agentId, opt =>
        {
            opt.MaxHistorySize = MaxHistorySize;
            opt.InstanceName = ServiceName;
            opt.InstanceType = GetType();
        }))
        {
            HeartbeatInterval = HeartbeatInterval
        };
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
        ObservableInfo.RecordStateChange(newState, message, exception);
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
