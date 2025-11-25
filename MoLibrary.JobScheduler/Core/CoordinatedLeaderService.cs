using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.Core;

/// <summary>
/// Abstract base class for background services that coordinate with RegisterCentre and only run on leader instances.
/// Handles registration wait, leader status verification, and initialization state management using the Template Method pattern.
/// </summary>
public abstract class CoordinatedLeaderService(
    ILeaderService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger logger,
    IServiceRegistrationCoordinator coordinator) : BackgroundService
{


    /// <summary>
    /// Module configuration options
    /// </summary>
    protected readonly ModuleJobSchedulerOption Options = options.Value;


    /// <summary>
    /// Gets a value indicating whether the service has completed initialization.
    /// Used by health checks to monitor service status.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the initialization error message if initialization failed.
    /// Null if initialization succeeded or has not completed yet.
    /// </summary>
    public string? InitializationError { get; private set; }

    /// <summary>
    /// Gets the name of the service for logging purposes.
    /// Should return the service class name (e.g., "JobSchedulerHostedService").
    /// </summary>
    protected abstract string ServiceName { get; }
    /// <summary>
    /// Executes the background service lifecycle using the Template Method pattern.
    /// This method is sealed to enforce consistent initialization sequence.
    /// </summary>
    /// <param name="stoppingToken">Triggered when the application host is performing a graceful shutdown</param>
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Step 1: Optional pre-initialization hook
            await OnBeforeInitialization(stoppingToken);

            // Step 2: Wait for RegisterCentre registration (if coordinator available)
            await WaitForRegistrationAsync(stoppingToken);

            // Step 3: Verify this instance is the leader
            if (!await EnsureIsLeaderAsync(stoppingToken))
            {
                // Follower instances mark as initialized without doing work
                IsInitialized = true;
                return;
            }

            // Step 4: Perform service-specific initialization (only on leader)
            await InitializeServiceAsync(stoppingToken);

            // Step 5: Mark as successfully initialized
            IsInitialized = true;
            logger.LogInformation("{ServiceName} initialized successfully", ServiceName);

            // Step 6: Optional post-initialization hook
            await OnAfterInitialization(stoppingToken);

            // // Step 7: Keep service running for event subscriptions, timers, etc.
            // await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown scenario - log at debug level
            logger.LogDebug("{ServiceName} background service is shutting down", ServiceName);
        }
        catch (Exception ex)
        {
            // Initialization failure - capture error and rethrow
            InitializationError = ex.Message;
            logger.LogError(ex, "{ServiceName} initialization failed", ServiceName);
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
            logger.LogInformation("{ServiceName} 正在等待注册中心注册完成...", ServiceName);

            var registered = await coordinator.WaitForRegistrationAsync(
                Options.RegistrationWaitTimeout,
                cancellationToken);

            if (registered)
            {
                logger.LogInformation("{ServiceName} 检测到注册完成，开始启动服务", ServiceName);
            }
            else
            {
                logger.LogWarning(
                    "{ServiceName} 等待注册超时({Timeout})，继续启动服务（降级模式）",
                    ServiceName,
                    Options.RegistrationWaitTimeout);
            }
        }
    }

    /// <summary>
    /// Checks if the current instance is the leader.
    /// Only leader instances should perform initialization.
    /// </summary>
    /// <returns>True if the instance is leader and should initialize, false otherwise</returns>
    private async Task<bool> EnsureIsLeaderAsync(CancellationToken cancellationToken)
    {
        var statusResult = await leaderService.GetCurrentLeaderStatusAsync();

        if (statusResult.IsFailed(out var error, out var data))
        {
            logger.LogError("Error getting leader status: {Error}", error);
            InitializationError = $"Failed to get leader status: {error.Message}";
            return false;
        }

        if (data.Status != LeaderStatus.Leader)
        {
            logger.LogInformation(
                "Not leader, current Leader status is {Status}, skip {ServiceName} initialization",
                data.Status,
                ServiceName);
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
    protected abstract Task InitializeServiceAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after successful initialization.
    /// Override this method for post-initialization setup (e.g., starting background tasks, additional subscriptions).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected virtual Task OnAfterInitialization(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
