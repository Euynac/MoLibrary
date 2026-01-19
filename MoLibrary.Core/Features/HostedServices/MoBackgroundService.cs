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
/// Base class for observable BackgroundService implementations with built-in state management,
/// exception tracking, and heartbeat monitoring.
/// Now uses ObservableAgent for unified tracking.
/// </summary>
public abstract class MoBackgroundService(
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> options,
    ILogger? logger = null) : BackgroundService, IMoHostedService
{
    protected readonly ILogger Logger = logger ?? NullLogger.Instance;
    private readonly ModuleHostedServiceOption _options = options.Value;

    // Heartbeat mechanism
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

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
    /// Gets the heartbeat interval for this service (null to disable heartbeat)
    /// </summary>
    public virtual TimeSpan? HeartbeatInterval => _options.DefaultHeartbeatInterval;

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
        var agent = observableManager.Create(agentId, opt =>
        {
            opt.MaxHistorySize = MaxHistorySize;
            opt.InstanceName = ServiceName;
            opt.InstanceType = GetType();
            opt.Logger = Logger;
        });

        // Configure log level mappings on the agent
        ConfigureStateLogLevels(agent);

        ObservableInfo = new HostedServiceObservableInfo(agent)
        {
            HeartbeatInterval = HeartbeatInterval
        };
    }

    /// <summary>
    /// Configures default log level mappings for HostedServiceState.
    /// Override to customize per service.
    /// </summary>
    protected virtual void ConfigureStateLogLevels(ObservableAgent agent)
    {
        agent.SetDebugStates(HostedServiceState.NotStarted, HostedServiceState.Starting);
        agent.SetInformationStates(
            HostedServiceState.Running,
            HostedServiceState.Executing,
            HostedServiceState.Stopping,
            HostedServiceState.Stopped
        );
        agent.SetWarningStates(HostedServiceState.Degraded);
        agent.SetErrorStates(HostedServiceState.Faulted);
    }

    /// <inheritdoc cref="ObservableAgent.RecordState" />
    protected void RecordState(string message,
        HostedServiceState? newState = null,
        Exception? exception = null,
        LogLevel? givenLogLevel = null)
    {
        ObservableInfo.Agent.RecordState(message, newState, exception, givenLogLevel);
    }

    /// <summary>
    /// Starts the heartbeat monitoring task
    /// </summary>
    private void StartHeartbeat()
    {
        if (HeartbeatInterval == null || HeartbeatInterval.Value <= TimeSpan.Zero)
            return;

        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = Task.Run(async () =>
        {
            while (!_heartbeatCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(HeartbeatInterval.Value, _heartbeatCts.Token);
                    ObservableInfo.LastHeartbeat = DateTime.UtcNow;
                    await OnHeartbeatAsync(_heartbeatCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "{ServiceName} heartbeat error", ServiceName);
                }
            }
        }, _heartbeatCts.Token);
    }

    /// <summary>
    /// Called on each heartbeat tick (override for custom heartbeat logic)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual Task OnHeartbeatAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Starts the background service
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordState("Service starting", HostedServiceState.Starting);
            ObservableInfo.StartedAt = DateTime.UtcNow;

            await base.StartAsync(cancellationToken);
            StartHeartbeat();

            RecordState("Service started, executing background work", HostedServiceState.Running);
        }
        catch (Exception ex)
        {
            RecordState("Service start failed", HostedServiceState.Faulted, ex);

            if (_options.FailFastOnStartupError)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Stops the background service
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordState("Service stopping", HostedServiceState.Stopping);

            if (_heartbeatCts?.Token.CanBeCanceled is true)
            {
                await _heartbeatCts.CancelAsync();
            }
            if (_heartbeatTask != null)
            {
                await _heartbeatTask;
            }

            await base.StopAsync(cancellationToken);

            ObservableInfo.StoppedAt = DateTime.UtcNow;
            RecordState("Service stopped", HostedServiceState.Stopped);
        }
        catch (Exception ex)
        {
            RecordState("Service stop failed", HostedServiceState.Faulted, ex);
        }
    }

    /// <summary>
    /// Wraps ExecuteAsync to track state and handle errors
    /// </summary>
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            RecordState("Executing background work", HostedServiceState.Executing);
            await ExecuteBackgroundAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("{ServiceName} background work cancelled", ServiceName);
        }
        catch (Exception ex)
        {
            RecordState("Background work failed", HostedServiceState.Faulted, ex);
        }
    }

    /// <summary>
    /// Executes the background work for this service (override in derived classes)
    /// </summary>
    /// <param name="stoppingToken">Triggered when the application host is performing a graceful shutdown</param>
    protected abstract Task ExecuteBackgroundAsync(CancellationToken stoppingToken);

    /// <summary>
    /// Disposes resources
    /// </summary>
    public override void Dispose()
    {
        if (_heartbeatCts?.Token.CanBeCanceled is true)
        {
            _heartbeatCts?.Cancel();
        }
        _heartbeatCts?.Dispose();
        base.Dispose();
    }
}
