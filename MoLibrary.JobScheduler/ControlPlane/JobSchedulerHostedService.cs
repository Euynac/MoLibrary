using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Modules;
using MoLibrary.Core.ObservableInstance;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Core;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.RegisterCentre.Interfaces;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Central orchestrator for job scheduling and execution requests.
/// Extends CoordinatedLeaderService for consistent initialization with RegisterCentre coordination and leader-only execution.
/// Coordinates RecurringJobScheduler and TriggeredJobScheduler.
/// </summary>
public class JobSchedulerHostedService(
    IOptions<ModuleJobSchedulerOption> options,
    RecurringJobScheduler recurringJobScheduler,
    TriggeredJobScheduler triggeredJobScheduler,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    ILeaderService leaderService,
    ILogger<JobSchedulerHostedService> logger,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions) : CoordinatedLeaderService(leaderService, options, logger, coordinator, observableManager, hostedServiceOptions)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;

    // Event subscriptions
    private IAsyncDisposable? _definitionsChangedSubscription;

    public override string ServiceName => nameof(JobSchedulerHostedService);

    protected override async Task InitializeServiceAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "JobScheduler starting. RecurringJobDebugMode: {RecurringDebug}, TriggeredJobDebugMode: {TriggeredDebug}",
            _options.RecurringJobDebugMode,
            _options.TriggeredJobDebugMode);

        // Initialize recurring job scheduler
        await recurringJobScheduler.InitializeAsync(_options.RecurringJobDebugMode, cancellationToken);

        // Initialize triggered job scheduler
        await triggeredJobScheduler.InitializeAsync(eventBus, _options.TriggeredJobDebugMode, cancellationToken);

        // Subscribe to job definitions changed event (recurring jobs only)
        _definitionsChangedSubscription = eventBus.Subscribe<JobDefinitionsChangedEvent>(
            evt => recurringJobScheduler.OnJobDefinitionsChangedAsync(evt));
        logger.LogDebug("Subscribed to JobDefinitionsChangedEvent");
    }

    /// <summary>
    /// Stops the job scheduler gracefully, coordinating shutdown of all schedulers.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobScheduler stopping...");

        try
        {
            // Call base to stop ExecuteAsync
            await base.StopAsync(cancellationToken);

            // Unsubscribe from events
            if (_definitionsChangedSubscription != null)
            {
                await _definitionsChangedSubscription.DisposeAsync();
            }

            // Stop schedulers
            await recurringJobScheduler.StopAsync(cancellationToken);
            await triggeredJobScheduler.StopAsync(cancellationToken);

            logger.LogInformation("JobScheduler stopped");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during JobScheduler shutdown");
            throw;
        }
    }
}
