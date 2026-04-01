using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Models;
using Monica.Core.Logging;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Core.ObservableInstance.Models;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Core.HostedService.Abstractions;

/// <summary>
/// Base class for observable BackgroundService implementations with built-in state management,
/// exception tracking, and heartbeat monitoring.
/// Now uses ObservableInstanceTracker for unified tracking.
/// </summary>
public abstract class MoBackgroundService : BackgroundService, IMoHostedService
{
    private readonly Lazy<ILogger> _loggerLazy;
    private readonly ModuleHostedServiceOption _options;
    private readonly IObservableInstanceRegistry _observableManager;

    // Heartbeat mechanism
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    protected MoBackgroundService(
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
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    public virtual int MaxHistorySize => _options.DefaultMaxHistorySize;

    /// <summary>
    /// Gets the heartbeat interval for this service (null to disable heartbeat)
    /// </summary>
    public virtual TimeSpan? HeartbeatInterval => _options.DefaultHeartbeatInterval;

    /// <summary>
    /// Gets the runtime information for this service.
    /// </summary>
    public HostedServiceRuntimeInfo RuntimeInfo => field ??= CreateRuntimeInfo();

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
        HostedServiceState? newState = null,
        Exception? exception = null,
        LogLevel? logLevel = null)
    {
        RuntimeInfo.Tracker.RecordState(message, newState, exception, logLevel);
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
                    RuntimeInfo.LastHeartbeat = DateTime.UtcNow;
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
            RuntimeInfo.StartedAt = DateTime.UtcNow;

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

            if (_heartbeatCts != null)
            {
                try { await _heartbeatCts.CancelAsync(); }
                catch (ObjectDisposedException) { }
            }
            if (_heartbeatTask != null)
            {
                await _heartbeatTask;
            }
            _heartbeatCts.SafeCancelAndDispose();
            _heartbeatCts = null;

            await base.StopAsync(cancellationToken);

            RuntimeInfo.StoppedAt = DateTime.UtcNow;
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
        _heartbeatCts.SafeCancelAndDispose();
        _heartbeatCts = null;
        base.Dispose();
    }
}
