using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.Core.HostedServices.Interfaces;
using MoLibrary.Core.HostedServices.Models;

namespace MoLibrary.Core.HostedServices;

/// <summary>
/// Base class for observable BackgroundService implementations with built-in state management,
/// exception tracking, and heartbeat monitoring
/// </summary>
public abstract class MoBackgroundService(
    IExceptionPoolManager? exceptionPoolManager = null,
    ILogger? logger = null) : BackgroundService, IMoHostedService
{
    protected readonly ILogger Logger = logger ?? NullLogger.Instance;

    // Heartbeat mechanism
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    // IMoHostedService implementation

    /// <summary>
    /// Gets the name of the service for identification purposes
    /// </summary>
    public abstract string ServiceName { get; }

    /// <summary>
    /// Gets a value indicating whether exception pool is enabled for this service
    /// </summary>
    public virtual bool EnableExceptionPool => true;

    /// <summary>
    /// Gets the maximum number of state history entries to retain
    /// </summary>
    public virtual int MaxHistorySize => 100;

    /// <summary>
    /// Gets the heartbeat interval for this service (null to disable heartbeat)
    /// </summary>
    public virtual TimeSpan? HeartbeatInterval => TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the observable information for this service
    /// </summary>
    public HostedServiceObservableInfo ObservableInfo { get; private set; } = null!;

    /// <summary>
    /// Gets the exception pool for this service (null if disabled)
    /// </summary>
    public ExceptionPool? ExceptionPool { get; private set; }

    /// <summary>
    /// Initializes observable info (called by manager during registration)
    /// </summary>
    internal void InitializeObservableInfo()
    {
        ObservableInfo = new HostedServiceObservableInfo
        {
            ServiceName = ServiceName,
            ServiceType = GetType(),
            RegisteredAt = DateTime.UtcNow,
            CurrentState = HostedServiceState.NotStarted,
            MaxHistorySize = MaxHistorySize,
            HeartbeatInterval = HeartbeatInterval
        };

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
        ObservableInfo.RecordStateChange(newState, message, exception);

        // Add to exception pool if error and pool is enabled
        if (exception != null && ExceptionPool != null)
        {
            ExceptionPool.AddException(exception, this, message);
        }
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
            RecordStateChange(HostedServiceState.Starting, "Service starting");
            ObservableInfo.StartedAt = DateTime.UtcNow;

            await base.StartAsync(cancellationToken);
            StartHeartbeat();

            RecordStateChange(HostedServiceState.Running, "Service started, executing background work");
        }
        catch (Exception ex)
        {
            RecordStateChange(HostedServiceState.Faulted, "Service start failed", ex);
            Logger.LogError(ex, "{ServiceName} failed to start", ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Stops the background service
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordStateChange(HostedServiceState.Stopping, "Service stopping");

            _heartbeatCts?.Cancel();
            if (_heartbeatTask != null)
            {
                await _heartbeatTask;
            }

            await base.StopAsync(cancellationToken);

            ObservableInfo.StoppedAt = DateTime.UtcNow;
            RecordStateChange(HostedServiceState.Stopped, "Service stopped");
        }
        catch (Exception ex)
        {
            RecordStateChange(HostedServiceState.Faulted, "Service stop failed", ex);
            Logger.LogError(ex, "{ServiceName} failed to stop gracefully", ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Wraps ExecuteAsync to track state and handle errors
    /// </summary>
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            RecordStateChange(HostedServiceState.Executing, "Executing background work");
            await ExecuteBackgroundAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("{ServiceName} background work cancelled", ServiceName);
        }
        catch (Exception ex)
        {
            RecordStateChange(HostedServiceState.Faulted, "Background work failed", ex);
            Logger.LogError(ex, "{ServiceName} background work failed", ServiceName);
            throw;
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
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        base.Dispose();
    }
}
