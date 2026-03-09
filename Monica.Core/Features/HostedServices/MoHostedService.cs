using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.Features.HostedServices.Interfaces;
using Monica.Core.Features.HostedServices.Models;
using Monica.Core.Features.ObservableInstance;
using Monica.Modules;

namespace Monica.Core.Features.HostedServices;

/// <summary>
/// Base class for observable IHostedService implementations with built-in state management and exception tracking.
/// Now uses ObservableAgent for unified tracking.
/// </summary>
public abstract class MoHostedService : IHostedService, IMoHostedService
{
    protected readonly ILogger Logger;
    private readonly ModuleHostedServiceOption _options;
    private readonly IObservableInstanceManager observableManager;

    public MoHostedService(
        IObservableInstanceManager observableManager,
        IOptions<ModuleHostedServiceOption> options,
        ILogger? logger = null)
    {
        this.observableManager = observableManager;
        Logger = logger ?? NullLogger.Instance;
        _options = options.Value;
        InitializeObservableInfo();
    }

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
    private void InitializeObservableInfo()
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
        HostedServiceState? newState,
        Exception? exception = null,
        LogLevel? logLevel = null)
    {
        ObservableInfo.Agent.RecordState(message, newState, exception, logLevel);
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

            // Only transition to Running if not already unhealthy
            if (!ObservableInfo.Agent.IsUnhealthy())
            {
                RecordState("Service started successfully", HostedServiceState.Running);
            }
            // If unhealthy, preserve the state set during OnStartingAsync

            await OnStartedAsync(cancellationToken);
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
