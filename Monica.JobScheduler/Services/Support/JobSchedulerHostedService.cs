using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Services.Support;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Central orchestrator for job scheduling and execution requests.
/// Extends CoordinatedLeaderService for consistent initialization with service registration coordination and leader-only execution.
/// Coordinates RecurringJobScheduler and TriggeredJobScheduler.
/// Supports dynamic leader status changes - stops schedulers on leader loss and re-initializes on leader gain.
/// </summary>
public class JobSchedulerHostedService(
    IOptions<ModuleJobSchedulerOption> options,
    RecurringJobScheduler recurringJobScheduler,
    TriggeredJobScheduler triggeredJobScheduler,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    IMoHostedServiceCheckpointCoordinator hostedServiceCheckpointCoordinator,
    ILeaderElectionService leaderService,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobSchedulerHostedService> logger)
    : CoordinatedLeaderService(leaderService, serviceDiscoveryOptions, coordinator, observableManager, hostedServiceOptions, serviceScopeFactory, logger)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;

    // Event subscriptions
    private IAsyncDisposable? _definitionsChangedSubscription;

    public override string ServiceName => nameof(JobSchedulerHostedService);
    public override string? ServiceGroupId => nameof(ModuleJobScheduler);

    protected override async Task OnBecameLeaderAsync(CancellationToken cancellationToken)
    {
        RecordState(
            $"Waiting for {nameof(JobDefinitionControlPlaneHostedService)} checkpoint '{JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady}'",
            HostedServiceState.WaitingDependency,
            logLevel: LogLevel.Information);

        await hostedServiceCheckpointCoordinator.WaitForCheckpointAsync<JobDefinitionControlPlaneHostedService>(
            JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady,
            LeaderService.LeaderBecomeTime,
            cancellationToken);

        RecordState(
            $"{nameof(JobDefinitionControlPlaneHostedService)} checkpoint '{JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady}' reached, continuing scheduler initialization",
            HostedServiceState.Executing,
            logLevel: LogLevel.Information);

        await recurringJobScheduler.InitializeAsync(cancellationToken);

        // Initialize triggered job scheduler
        await triggeredJobScheduler.InitializeAsync(eventBus, cancellationToken);

        // Subscribe to job definitions changed event (recurring jobs only)
        _definitionsChangedSubscription = await eventBus.SubscribeAsync<JobDefinitionsChangedEvent>(
            recurringJobScheduler.OnJobDefinitionsChangedAsync,
            JobEventTopicHelper.GetTopicName<JobDefinitionsChangedEvent>(_options.SchedulerScopeKey));

        hostedServiceCheckpointCoordinator.SignalCheckpoint(
            this,
            JobSchedulerHostedServiceCheckpoints.SchedulerReady);
        RecordState("Subscribed to JobDefinitionsChangedEvent", logLevel: LogLevel.Debug);
    }

    /// <summary>
    /// Cleans up schedulers and event subscriptions when leader status is lost.
    /// This allows for proper re-initialization when leader status is re-gained.
    /// </summary>
    protected override async Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        RecordState($"JobScheduler cleaning up after losing leader status (reason: {reason})", logLevel: LogLevel.Information);

        // Unsubscribe from events
        if (_definitionsChangedSubscription != null)
        {
            await _definitionsChangedSubscription.DisposeAsync();
            _definitionsChangedSubscription = null;
        }

        // Stop schedulers (they will be re-initialized when leader status is re-gained)
        await recurringJobScheduler.StopAsync(CancellationToken.None);
        await triggeredJobScheduler.StopAsync(CancellationToken.None);

        RecordState("JobScheduler cleanup completed", logLevel: LogLevel.Information);
    }
}
