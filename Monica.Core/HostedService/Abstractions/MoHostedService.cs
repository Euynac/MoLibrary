using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Monica.Core.HostedService.Models;
using Monica.Core.Logging;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Core.ObservableInstance.Models;
using Monica.Modules;

namespace Monica.Core.HostedService.Abstractions;

/// <summary>
/// Base class for observable IHostedService implementations with built-in state management and exception tracking.
/// Now uses ObservableInstanceTracker for unified tracking.
/// </summary>
public abstract class MoHostedService : IHostedService, IMoHostedService
{
    private readonly Lazy<ILogger> _loggerLazy;
    private readonly ModuleHostedServiceOption _options;
    private readonly IObservableInstanceRegistry _observableManager;
    private HostedServiceRuntimeInfo? _runtimeInfo;

    public MoHostedService(
        IObservableInstanceRegistry observableManager,
        IOptions<ModuleHostedServiceOption> options)
    {
        _observableManager = observableManager;
        _options = options.Value;
        _loggerLazy = new Lazy<ILogger>(() => LogManager.For(GetType()));
    }

    protected ILogger Logger => _loggerLazy.Value;

    // IMoHostedService implementation

    /// <summary>
    /// Gets the name of the service for identification purposes
    /// </summary>
    public virtual string ServiceName => GetType().Name;

    /// <summary>
    /// Gets the observable group identifier used to group related hosted services.
    /// Return null to leave the service ungrouped.
    /// </summary>
    public virtual string? ServiceGroupId => null;

    /// <summary>
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    public virtual int MaxHistorySize => _options.DefaultMaxHistorySize;

    /// <summary>
    /// Gets the heartbeat interval (always null for MoHostedService, only applicable to MoBackgroundService)
    /// </summary>
    public virtual TimeSpan? HeartbeatInterval => null;

    /// <summary>
    /// Gets the runtime information for this service.
    /// </summary>
    public HostedServiceRuntimeInfo RuntimeInfo => _runtimeInfo ??= CreateRuntimeInfo();

    /// <summary>
    /// Creates runtime information lazily after the derived hosted service has finished construction.
    /// </summary>
    private HostedServiceRuntimeInfo CreateRuntimeInfo()
    {
        var trackerId = $"HostedService_{ServiceName}_{Guid.NewGuid():N}";
        var tracker = _observableManager.Register(trackerId, opt =>
        {
            opt.MaxHistorySize = MaxHistorySize;
            opt.InstanceName = ServiceName;
            opt.InstanceType = GetType();
            opt.GroupId = ServiceGroupId;
            opt.Logger = Logger;
        });

        ConfigureStateLogLevels(tracker);

        return new HostedServiceRuntimeInfo(tracker)
        {
            HeartbeatInterval = HeartbeatInterval
        };
    }

    /// <summary>
    /// Configures default log level mappings for HostedServiceState.
    /// Override to customize per service.
    /// </summary>
    protected virtual void ConfigureStateLogLevels(ObservableInstanceTracker tracker)
    {
        tracker.SetDebugStates(HostedServiceState.NotStarted, HostedServiceState.Starting);
        tracker.SetInformationStates(
            HostedServiceState.Running,
            HostedServiceState.Executing,
            HostedServiceState.WaitingDependency,
            HostedServiceState.Stopping,
            HostedServiceState.Stopped
        );
        tracker.SetWarningStates(HostedServiceState.Degraded);
        tracker.SetErrorStates(HostedServiceState.Faulted);
    }

    /// <inheritdoc cref="ObservableInstanceTracker.RecordState" />
    protected void RecordState(string message,
        HostedServiceState? newState,
        Exception? exception = null,
        LogLevel? logLevel = null)
    {
        RuntimeInfo.Tracker.RecordState(message, newState, exception, logLevel);
    }

    /// <summary>
    /// Starts the hosted service
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordState("Service starting", HostedServiceState.Starting);
            RuntimeInfo.StartedAt = DateTime.UtcNow;

            await OnStartingAsync(cancellationToken);

            // Only transition to Running if not already unhealthy
            if (!RuntimeInfo.Tracker.IsUnhealthy())
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

            RuntimeInfo.StoppedAt = DateTime.UtcNow;
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
