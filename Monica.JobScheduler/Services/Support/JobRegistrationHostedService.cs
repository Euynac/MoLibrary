using System.Reflection;
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
/// Leader-only control-plane service that reconciles discovered job definitions with persistent metadata and publishes
/// definition-change events. Local CLR-type registration is owned by <see cref="JobRegistry"/> on every host and does
/// not depend on leadership.
/// </summary>
public class JobRegistrationHostedService(
    JobRegistry jobRegistry,
    IReadOnlyList<JobDefinition> jobDefinitions,
    IMoHostedServiceCheckpointCoordinator hostedServiceCheckpointCoordinator,
    ILeaderElectionService leaderService,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    IOptions<ModuleJobSchedulerOption> option,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobRegistrationHostedService> logger)
    : CoordinatedLeaderService(leaderService, serviceDiscoveryOptions, coordinator, observableManager, hostedServiceOptions, serviceScopeFactory, logger)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = option.Value;

    public override string ServiceName => nameof(JobRegistrationHostedService);
    public override string? ServiceGroupId => nameof(ModuleJobScheduler);

    /// <summary>
    /// Logs when leader status is lost. Job registration is a one-time operation per leader election,
    /// so no cleanup is needed. The jobs will be re-registered when leader status is re-gained.
    /// </summary>
    protected override Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        RecordState($"Job registration service lost leader status (reason: {reason}). Jobs will be re-registered on next leader election.", logLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    protected override async Task OnBecameLeaderAsync(CancellationToken cancellationToken)
    {
        // Perform job registration
        await RegisterJobsAsync(cancellationToken);
    }

    private async Task RegisterJobsAsync(CancellationToken cancellationToken)
    {
        RecordState($"Starting job reconciliation for {jobDefinitions.Count} job(s)", logLevel: LogLevel.Information);

        // Reconcile job definitions (add new jobs and soft delete removed jobs)
        var result = await jobRegistry.ReconcileJobDefinitionsAsync(jobDefinitions, cancellationToken);

        // Log reconciliation summary
        RecordState(
            $"Job reconciliation completed: {result.AddedCount} job(s) added, {result.DeletedCount} job(s) soft deleted, {jobDefinitions.Count - result.AddedCount} job(s) unchanged",
            logLevel: LogLevel.Information);

        // Log deleted jobs if any
        if (result.DeletedCount > 0)
        {
            foreach (var deletedKey in result.DeletedJobKeys)
            {
                RecordState($"[Deleted] Job: {deletedKey} - No longer exists in code", logLevel: LogLevel.Warning);
            }
        }

        hostedServiceCheckpointCoordinator.SignalCheckpoint(
            this,
            JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady);

        RecordState(
            $"Signaled hosted service checkpoint: {JobSchedulerHostedServiceCheckpoints.JobDefinitionsReady}",
            logLevel: LogLevel.Information);

        if (_jobSchedulerOptions.RecurringJobDebugMode)
        {
            RecordState("Received JobDefinitionsChangedEvent in debug mode, skipping schedule updates", logLevel: LogLevel.Debug);
            return;
        }

        // Publish JobDefinitionsChangedEvent to notify subscribers
        try
        {
            if (result is { AddedCount: 0, DeletedCount: 0 })
            {
                RecordState("No job definitions changed, skipping event publication", logLevel: LogLevel.Information);
                return;
            }

            await eventBus.PublishAsync(new JobDefinitionsChangedEvent
            {
                SchedulerScopeKey = _jobSchedulerOptions.SchedulerScopeKey,
                FromProject = Assembly.GetEntryAssembly()?.GetName().Name!,
                AddedDefinitions = jobDefinitions.Where(d => result.AddedJobKeys.Contains(d.JobKey)).ToList(),
                AddedJobKeys = result.AddedJobKeys.ToList(),
                DeletedJobKeys = result.DeletedJobKeys.ToList(),
                UpdatedDefinitions = [],  // Reconciliation does not handle updates
                UpdatedJobKeys = [],      // Reconciliation does not handle updates
                ReconciledAt = DateTime.UtcNow
            }, JobEventTopicHelper.GetTopicName<JobDefinitionsChangedEvent>(_jobSchedulerOptions.SchedulerScopeKey), cancellationToken);

            RecordState("Published JobDefinitionsChangedEvent to notify subscribers of reconciliation", logLevel: LogLevel.Information);
        }
        catch (Exception eventEx)
        {
            // Log error but don't fail startup if event publishing fails
            RecordState("Failed to publish JobDefinitionsChangedEvent, but continuing startup", logLevel: LogLevel.Error, exception: eventEx);
        }
    }
}
