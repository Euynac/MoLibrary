using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices.Interfaces;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;

namespace MoLibrary.Core.Features.HostedServices;

/// <summary>
/// Base class for observable IHostedService implementations with built-in state management and exception tracking.
/// Now uses ObservableAgent for unified tracking.
/// </summary>
public abstract class MoHostedService(
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> options,
    ILogger? logger = null) : IHostedService, IMoHostedService
{
    protected readonly ILogger Logger = logger ?? NullLogger.Instance;
    private readonly ModuleHostedServiceOption _options = options.Value;

    // IMoHostedService implementation

    /// <summary>
    /// Gets the name of the service for identification purposes
    /// </summary>
    public abstract string ServiceName { get; }

    /// <summary>
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    public virtual int MaxHistorySize => _options.DefaultMaxHistorySize;

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

    /// <inheritdoc cref="ObservableAgent.RecordState" />
    protected void RecordState(string message,
        HostedServiceState? newState,
        Exception? exception = null)
    {
        ObservableInfo.RecordState(message, newState, exception);
    }

    /// <summary>
    /// Starts the hosted service
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordState("Service starting", HostedServiceState.Starting);
            ObservableInfo.StartedAt = DateTime.UtcNow;

            await OnStartingAsync(cancellationToken);

            RecordState("Service started successfully", HostedServiceState.Running);

            await OnStartedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            RecordState("Service start failed", HostedServiceState.Faulted, ex);
            Logger.LogError(ex, "{ServiceName} failed to start", ServiceName);

            if (_options.FailFastOnStartupError)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Stops the hosted service
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordState("Service stopping", HostedServiceState.Stopping);

            await OnStoppingAsync(cancellationToken);

            ObservableInfo.StoppedAt = DateTime.UtcNow;
            RecordState("Service stopped successfully", HostedServiceState.Stopped);

            await OnStoppedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            RecordState("Service stop failed", HostedServiceState.Faulted, ex);
            Logger.LogError(ex, "{ServiceName} failed to stop gracefully", ServiceName);
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
