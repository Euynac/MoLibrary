using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;

namespace MoLibrary.JobScheduler.Core;

/// <summary>
/// Abstract base class for background services that coordinate with RegisterCentre and only run on leader instances.
/// Handles registration wait, leader status verification, and initialization state management using the Template Method pattern.
/// </summary>
public abstract class CoordinatedLeaderService(
    ILeaderElectionService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger logger,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions) : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    /// <summary>
    /// Module configuration options
    /// </summary>
    protected readonly ModuleJobSchedulerOption Options = options.Value;

    /// <summary>
    /// Gets a value indicating whether the service has completed initialization.
    /// Used by health checks to monitor service status.
    /// </summary>
    public bool IsInitialized => ObservableInfo is {CurrentState: HostedServiceState.Running or HostedServiceState.Executing};

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
    /// This method is sealed to enforce consistent initialization sequence.
    /// </summary>
    /// <param name="stoppingToken">Triggered when the application host is performing a graceful shutdown</param>
    protected sealed override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Step 1: Optional pre-initialization hook
            RecordState("Pre-initialization starting", HostedServiceState.Starting);
            await OnBeforeInitialization(stoppingToken);

            // Step 2: Wait for RegisterCentre registration (if coordinator available)
            RecordState("Waiting for registration", HostedServiceState.Starting);
            await WaitForRegistrationAsync(stoppingToken);

            // Step 3: Verify this instance is the leader
            RecordState("Checking leader status", HostedServiceState.Starting);
            if (!EnsureIsLeader())
            {
                // Follower instances mark as running without doing work
                RecordState("Follower instance - no work to do", HostedServiceState.Running);
                return;
            }

            // Step 4: Perform service-specific initialization (only on leader)
            RecordState("Initializing as leader", HostedServiceState.Executing);
            await LeaderInitializeAsync(stoppingToken);

            // Step 5: Mark as successfully initialized
            RecordState("Leader initialized successfully", HostedServiceState.Running);

            // Step 6: Optional post-initialization hook
            await LeaderExecuteBackgroundAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown scenario - log at debug level
            Logger.LogDebug("{ServiceName} background service is shutting down", ServiceName);
        }
        catch (Exception ex)
        {
            // Initialization failure - capture error and rethrow
            RecordState("Initialization failed", HostedServiceState.Faulted, ex);
            throw; // Rethrow to let the host handle the failure
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
    /// Checks if the current instance is the leader.
    /// Only leader instances should perform initialization.
    /// </summary>
    /// <returns>True if the instance is leader and should initialize, false otherwise</returns>
    private bool EnsureIsLeader()
    {
        var status = leaderService.CurrentStatus;

        if (status != LeaderStatus.Leader)
        {
            RecordState($"Not leader (status: {status}), skipping initialization", HostedServiceState.Starting);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Called before registration wait and leader check.
    /// Override this method for pre-initialization setup (e.g., loading configuration, preparing resources).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected virtual Task OnBeforeInitialization(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Performs service-specific initialization logic.
    /// Called only on leader instances after registration wait completes successfully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping initialization</param>
    /// <returns>A task representing the asynchronous initialization</returns>
    /// <remarks>
    /// This method is called within the try-catch block of ExecuteAsync.
    /// Exceptions thrown here will be caught, logged, and cause initialization to fail.
    /// </remarks>
    protected abstract Task LeaderInitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after successful initialization.
    /// Override this method for post-initialization setup (e.g., starting background tasks, additional subscriptions).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected virtual Task LeaderExecuteBackgroundAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
