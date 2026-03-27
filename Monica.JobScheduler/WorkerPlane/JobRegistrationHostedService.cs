using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Features.ObservableInstance;
using Monica.Core.HostedService.Abstractions;
using Monica.Modules;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.ControlPlane;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Helpers;
using Monica.JobScheduler.Models;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Services.Support;

namespace Monica.JobScheduler.WorkerPlane;

/// <summary>
/// Background service that registers discovered job definitions to the JobRegistry during application startup.
/// Extends CoordinatedLeaderService for consistent initialization with service registration coordination and leader-only execution.
/// Publishes JobDefinitionsChangedEvent after reconciliation to notify subscribers of changes.
/// Supports dynamic leader status changes - re-registers job definitions when leader status is re-gained.
/// </summary>
public class JobRegistrationHostedService(
    JobRegistry jobRegistry,
    IReadOnlyList<JobDefinition> jobDefinitions,
    ILogger<JobRegistrationHostedService> logger,
    IMoHostedServiceCheckpointCoordinator hostedServiceCheckpointCoordinator,
    ILeaderElectionService leaderService,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    IOptions<ModuleJobSchedulerOption> option,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions) : CoordinatedLeaderService(leaderService, serviceDiscoveryOptions, logger, coordinator, observableManager, hostedServiceOptions)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = option.Value;

    public override string ServiceName => nameof(JobRegistrationHostedService);

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

        // Register all current job definitions
        foreach (var definition in jobDefinitions)
        {
            var status = result.AddedJobKeys.Contains(definition.JobKey) ? "Added" : "Already Registered";
            await jobRegistry.RegisterJob(definition, status);
        }

        hostedServiceCheckpointCoordinator.SignalCheckpoint<JobRegistrationHostedService>(
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
