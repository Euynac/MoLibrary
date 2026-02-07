using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Core.Features.HostedServices;
using Monica.Core.Features.HostedServices.Models;
using Monica.Core.Features.ObservableInstance;
using Monica.Core.Modules;
using Monica.RegisterCentre.Events;
using Monica.RegisterCentre.Interfaces;
using Monica.RegisterCentre.Modules;
using Monica.Tool.Extensions;

namespace Monica.RegisterCentre.Core;

/// <summary>
/// Abstract base class for background services that coordinate with RegisterCentre and only run on leader instances.
/// Supports dynamic leader status changes through event subscriptions, enabling services to react to leader gain/loss.
/// Uses the Template Method pattern for consistent lifecycle management.
/// </summary>
public abstract class CoordinatedLeaderService(
    ILeaderElectionService leaderService,
    IOptions<ModuleRegisterCentreOption> options,
    ILogger logger,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions) : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    /// <summary>
    /// Module configuration options
    /// </summary>
    protected readonly ModuleRegisterCentreOption Options = options.Value;

    /// <summary>
    /// Leader election service for status tracking and event subscriptions
    /// </summary>
    protected readonly ILeaderElectionService LeaderService = leaderService;

    /// <summary>
    /// Cancellation token source for leader-scoped operations.
    /// Cancelled when the instance loses leader status.
    /// </summary>
    private CancellationTokenSource? _leaderCts;

    /// <summary>
    /// Task tracking the background execution started when becoming leader.
    /// </summary>
    private Task? _leaderBackgroundTask;

    /// <summary>
    /// Lock object for thread-safe leader status transitions.
    /// </summary>
    private readonly SemaphoreSlim _leaderTransitionLock = new(1, 1);

    /// <summary>
    /// The application-level stopping token passed from ExecuteBackgroundAsync.
    /// </summary>
    private CancellationToken _stoppingToken;

    /// <summary>
    /// Gets a value indicating whether the service has completed initialization.
    /// Used by health checks to monitor service status.
    /// </summary>
    public bool IsInitialized => ObservableInfo is { CurrentState: HostedServiceState.Running or HostedServiceState.Executing };

    /// <summary>
    /// Gets a value indicating whether this instance is currently the leader.
    /// </summary>
    public bool IsCurrentlyLeader => LeaderService.IsLeader;

    /// <summary>
    /// Gets the initialization error message if initialization failed.
    /// Null if initialization succeeded or has not completed yet.
    /// </summary>
    public string? InitializationError =>
        ObservableInfo.StateHistory
            .Where(h => h.Exception != null)
            .OrderByDescending(h => h.Timestamp)
            .FirstOrDefault()
            ?.Exception?.GetMessageRecursively();

    /// <summary>
    /// Executes the background service lifecycle using the Template Method pattern.
    /// Subscribes to leader events and reacts to dynamic leader status changes.
    /// This method is sealed to enforce consistent initialization sequence.
    /// </summary>
    /// <param name="stoppingToken">Triggered when the application host is performing a graceful shutdown</param>
    protected sealed override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        try
        {
            // Step 1: Optional pre-initialization hook
            RecordState("Pre-initialization starting", HostedServiceState.Starting);
            await OnBeforeInitialization(stoppingToken);

            // Step 2: Wait for RegisterCentre registration (if coordinator available)
            RecordState("Waiting for registration", HostedServiceState.Starting);
            await WaitForRegistrationAsync(stoppingToken);

            // Step 3: Subscribe to leader status change events
            RecordState("Subscribing to leader events", HostedServiceState.Starting);
            LeaderService.OnLeaderGained += OnLeaderGainedHandler;
            LeaderService.OnLeaderLost += OnLeaderLostHandler;

            // Step 4: Check if already leader and trigger initial leader gained
            if (LeaderService.IsLeader)
            {
                RecordState("Already leader, triggering initial leader gained", HostedServiceState.Starting);
                await HandleLeaderGainedAsync();
            }
            else
            {
                RecordState($"Not leader (status: {LeaderService.CurrentStatus}), waiting for leader election", HostedServiceState.Running);
            }

            // Step 5: Wait until the application is stopping
            // Leader work is handled by event handlers
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown scenario - log at debug level
            Logger.LogDebug("{ServiceName} background service is shutting down", ServiceName);
        }
        catch (Exception ex)
        {
            // Initialization failure - capture error and rethrow
            RecordState("Service failed", HostedServiceState.Faulted, ex);
            throw;
        }
        finally
        {
            // Cleanup: unsubscribe from events
            LeaderService.OnLeaderGained -= OnLeaderGainedHandler;
            LeaderService.OnLeaderLost -= OnLeaderLostHandler;

            // Ensure leader resources are cleaned up
            await CleanupLeaderResourcesAsync(LeaderLostReason.GracefulShutdown);
        }
    }

    /// <summary>
    /// Event handler for leader gained event.
    /// Delegates to async handler on thread pool to avoid blocking the event source.
    /// </summary>
    private void OnLeaderGainedHandler(object? sender, LeaderGainedEvent e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleLeaderGainedAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{ServiceName} failed to handle leader gained event", ServiceName);
                RecordState("Failed to handle leader gained", HostedServiceState.Faulted, ex);
            }
        });
    }

    /// <summary>
    /// Event handler for leader lost event.
    /// Delegates to async handler on thread pool to avoid blocking the event source.
    /// </summary>
    private void OnLeaderLostHandler(object? sender, LeaderLostEvent e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleLeaderLostAsync(e.Reason);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{ServiceName} failed to handle leader lost event", ServiceName);
            }
        });
    }

    /// <summary>
    /// Handles the transition to leader status.
    /// Initializes leader-scoped resources and starts background work.
    /// </summary>
    private async Task HandleLeaderGainedAsync()
    {
        await _leaderTransitionLock.WaitAsync(_stoppingToken);
        try
        {
            // Check if already running as leader (avoid re-entrancy)
            if (_leaderCts is { IsCancellationRequested: false })
            {
                Logger.LogDebug("{ServiceName} already running as leader, ignoring duplicate leader gained event", ServiceName);
                return;
            }

            // Check if application is shutting down
            if (_stoppingToken.IsCancellationRequested)
            {
                Logger.LogDebug("{ServiceName} application is shutting down, ignoring leader gained event", ServiceName);
                return;
            }

            RecordState("Becoming leader", HostedServiceState.Executing);

            // Create leader-scoped cancellation token
            _leaderCts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken);
            var leaderToken = _leaderCts.Token;

            // Perform service-specific initialization
            RecordState("Initializing as leader", HostedServiceState.Executing);
            await LeaderInitializeAsync(leaderToken);

            // Call post-initialization hook
            await OnBecameLeaderAsync(leaderToken);

            RecordState("Leader initialized successfully", HostedServiceState.Running);

            // Start background work in a separate task
            _leaderBackgroundTask = Task.Run(async () =>
            {
                try
                {
                    await LeaderExecuteBackgroundAsync(leaderToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when leader token is cancelled
                    Logger.LogDebug("{ServiceName} leader background task cancelled", ServiceName);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{ServiceName} leader background task failed", ServiceName);
                    RecordState("Leader background task failed", HostedServiceState.Faulted, ex);
                }
            }, leaderToken);
        }
        finally
        {
            _leaderTransitionLock.Release();
        }
    }

    /// <summary>
    /// Handles the loss of leader status.
    /// Cancels leader-scoped operations and performs cleanup.
    /// </summary>
    private async Task HandleLeaderLostAsync(LeaderLostReason reason)
    {
        await _leaderTransitionLock.WaitAsync();
        try
        {
            RecordState($"Lost leader status (reason: {reason})", HostedServiceState.Starting);
            await CleanupLeaderResourcesAsync(reason);
            RecordState("Leader cleanup completed, waiting for re-election", HostedServiceState.Running);
        }
        finally
        {
            _leaderTransitionLock.Release();
        }
    }

    /// <summary>
    /// Cleans up leader-scoped resources.
    /// </summary>
    private async Task CleanupLeaderResourcesAsync(LeaderLostReason reason)
    {
        // Cancel leader-scoped operations
        if (_leaderCts != null)
        {
            try { await _leaderCts.CancelAsync(); }
            catch (ObjectDisposedException) { }

            // Wait for background task to complete
            if (_leaderBackgroundTask != null)
            {
                try
                {
                    await _leaderBackgroundTask.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    Logger.LogWarning("{ServiceName} leader background task did not complete within timeout", ServiceName);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }

                _leaderBackgroundTask = null;
            }

            _leaderCts.SafeCancelAndDispose();
            _leaderCts = null;
        }

        // Call subclass cleanup hook
        try
        {
            await OnLeaderLostAsync(reason);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{ServiceName} failed during OnLeaderLostAsync", ServiceName);
        }
    }

    /// <summary>
    /// Waits for RegisterCentre registration to complete.
    /// Respects SkipRegistrationWait option for development/testing scenarios.
    /// </summary>
    private async Task WaitForRegistrationAsync(CancellationToken cancellationToken)
    {
        if (!Options.SkipRegistrationWait)
        {
            RecordState("Waiting for RegisterCentre registration", HostedServiceState.Starting);

            var registered = await coordinator.WaitForRegistrationAsync(
                Options.RegistrationWaitTimeout,
                cancellationToken);

            if (registered)
            {
                RecordState("Registration completed", HostedServiceState.Starting);
            }
            else
            {
                RecordState($"Registration timeout ({Options.RegistrationWaitTimeout}), starting in degraded mode", HostedServiceState.Starting);
            }
        }
    }

    /// <summary>
    /// Called before registration wait and leader event subscription.
    /// Override this method for pre-initialization setup (e.g., loading configuration, preparing resources).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected virtual Task OnBeforeInitialization(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Performs service-specific initialization logic.
    /// Called when this instance becomes the leader.
    /// May be called multiple times if leader status is lost and re-gained.
    /// </summary>
    /// <param name="leaderToken">Cancellation token that is cancelled when leader status is lost</param>
    /// <returns>A task representing the asynchronous initialization</returns>
    /// <remarks>
    /// Implementations should be idempotent or reset state appropriately,
    /// as this method may be called multiple times during the service lifetime.
    /// </remarks>
    protected abstract Task LeaderInitializeAsync(CancellationToken leaderToken);

    /// <summary>
    /// Called immediately after successful leader initialization.
    /// Override this method for post-initialization setup that depends on being the leader.
    /// </summary>
    /// <param name="leaderToken">Cancellation token that is cancelled when leader status is lost</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected virtual Task OnBecameLeaderAsync(CancellationToken leaderToken)
        => Task.CompletedTask;

    /// <summary>
    /// Executes background work while this instance is the leader.
    /// Override this method for long-running background tasks (e.g., periodic scanning).
    /// The task is automatically cancelled when leader status is lost.
    /// </summary>
    /// <param name="leaderToken">Cancellation token that is cancelled when leader status is lost</param>
    /// <returns>A task representing the background work</returns>
    protected virtual Task LeaderExecuteBackgroundAsync(CancellationToken leaderToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called when this instance loses leader status.
    /// Override this method for cleanup (e.g., disposing subscriptions, stopping schedulers).
    /// </summary>
    /// <param name="reason">The reason for losing leader status</param>
    /// <returns>A task representing the cleanup operation</returns>
    protected virtual Task OnLeaderLostAsync(LeaderLostReason reason)
        => Task.CompletedTask;
}
