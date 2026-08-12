using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
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
/// Owns definition reconciliation and publication subscriptions on the elected scheduler control-plane instance.
/// </summary>
internal sealed class JobDefinitionControlPlaneHostedService(
    JobDefinitionReconciler reconciler,
    IReadOnlyList<JobDefinition> localDefinitions,
    IMoHostedServiceCheckpointCoordinator hostedServiceCheckpointCoordinator,
    ILeaderElectionService leaderService,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    IServiceDiscoveryClientInfo clientInfo,
    IOptions<ModuleJobSchedulerOption> options,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobDefinitionControlPlaneHostedService> logger)
    : CoordinatedLeaderService(
        leaderService,
        serviceDiscoveryOptions,
        coordinator,
        observableManager,
        hostedServiceOptions,
        serviceScopeFactory,
        logger)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;
    private IAsyncDisposable? _snapshotSubscription;

    public override string? ServiceGroupId => nameof(ModuleJobScheduler);

    protected override async Task OnBecameLeaderAsync(CancellationToken leaderToken)
    {
        _snapshotSubscription = await eventBus.SubscribeAsync<JobDefinitionSnapshotPublishedEvent>(
            async (snapshot, deliveryToken) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(leaderToken, deliveryToken);
                linked.Token.ThrowIfCancellationRequested();
                await WaitForConsumersAsync(linked.Token);
                await ApplySnapshotAsync(snapshot, linked.Token);
            },
            JobEventTopicHelper.GetTopicName<JobDefinitionSnapshotPublishedEvent>(
                _options.SchedulerScopeKey));

        var service = clientInfo.GetServiceStatus(isHeartbeatInfo: false);
        var localSnapshot = JobDefinitionSnapshotPublishedEvent.Create(
            _options.SchedulerScopeKey,
            _options.GetProjectName(),
            service.AppId,
            service.InstanceId,
            service.BuildTime,
            service.ReleaseVersion,
            localDefinitions);
        await ApplySnapshotAsync(localSnapshot, leaderToken);

        hostedServiceCheckpointCoordinator.SignalCheckpoint(
            this,
            JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady);
        RecordState(
            $"Definition catalog is ready and worker snapshots are accepted on scope '{_options.SchedulerScopeKey}'",
            logLevel: LogLevel.Information);
    }

    protected override async Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        if (Interlocked.Exchange(ref _snapshotSubscription, null) is { } subscription)
        {
            await subscription.DisposeAsync();
        }

        RecordState(
            $"Stopped definition reconciliation after losing leadership ({reason})",
            logLevel: LogLevel.Information);
    }

    private Task ApplySnapshotAsync(
        JobDefinitionSnapshotPublishedEvent snapshot,
        CancellationToken cancellationToken)
    {
        return reconciler.ApplyAsync(snapshot, PublishChangesAsync, cancellationToken);
    }

    private async Task WaitForConsumersAsync(CancellationToken cancellationToken)
    {
        await hostedServiceCheckpointCoordinator.WaitForCheckpointAsync<JobSchedulerHostedService>(
            JobSchedulerHostedServiceCheckpoints.SchedulerReady,
            LeaderService.LeaderBecomeTime,
            cancellationToken);
        await hostedServiceCheckpointCoordinator.WaitForCheckpointAsync<JobConcurrencyGuardHostedService>(
            JobSchedulerHostedServiceCheckpoints.ConcurrencyGuardReady,
            LeaderService.LeaderBecomeTime,
            cancellationToken);
    }

    private async Task PublishChangesAsync(
        JobDefinitionSnapshotApplyResult result,
        CancellationToken cancellationToken)
    {
        await eventBus.PublishAsync(
            new JobDefinitionsChangedEvent
            {
                SchedulerScopeKey = _options.SchedulerScopeKey,
                FromProject = result.Snapshot.OwnerProject,
                AddedDefinitions = result.AddedDefinitions,
                AddedJobKeys = result.AddedDefinitions.Select(static definition => definition.JobKey).ToArray(),
                UpdatedDefinitions = result.UpdatedDefinitions,
                UpdatedJobKeys = result.UpdatedDefinitions.Select(static definition => definition.JobKey).ToArray(),
                DeletedJobKeys = result.DeletedJobKeys,
                ReconciledAt = DateTime.UtcNow
            },
            JobEventTopicHelper.GetTopicName<JobDefinitionsChangedEvent>(_options.SchedulerScopeKey),
            cancellationToken);
    }
}
