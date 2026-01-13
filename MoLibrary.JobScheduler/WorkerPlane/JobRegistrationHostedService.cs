using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.RegisterCentre.Core;
using MoLibrary.RegisterCentre.Events;
using MoLibrary.RegisterCentre.Interfaces;

namespace MoLibrary.JobScheduler.WorkerPlane;

/// <summary>
/// Background service that registers discovered job definitions to the JobRegistry during application startup.
/// Extends CoordinatedLeaderService for consistent initialization with RegisterCentre coordination and leader-only execution.
/// Publishes JobDefinitionsChangedEvent after reconciliation to notify subscribers of changes.
/// Supports dynamic leader status changes - re-registers job definitions when leader status is re-gained.
/// </summary>
public class JobRegistrationHostedService(
    JobRegistry jobRegistry,
    IReadOnlyList<JobDefinition> jobDefinitions,
    ILogger<JobRegistrationHostedService> logger,
    ILeaderElectionService leaderService,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    IOptions<ModuleJobSchedulerOption> option,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleRegisterCentreOption> registerCentreOptions) : CoordinatedLeaderService(leaderService, registerCentreOptions, logger, coordinator, observableManager, hostedServiceOptions)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = option.Value;

    public override string ServiceName => nameof(JobRegistrationHostedService);

    /// <summary>
    /// Logs when leader status is lost. Job registration is a one-time operation per leader election,
    /// so no cleanup is needed. The jobs will be re-registered when leader status is re-gained.
    /// </summary>
    protected override Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        logger.LogInformation("Job registration service lost leader status (reason: {Reason}). Jobs will be re-registered on next leader election.", reason);
        return Task.CompletedTask;
    }

    protected override async Task LeaderInitializeAsync(CancellationToken cancellationToken)
    {
        // Perform job registration
        await RegisterJobsAsync(cancellationToken);
    }

    private async Task RegisterJobsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting job reconciliation for {Count} job(s)", jobDefinitions.Count);

        // Reconcile job definitions (add new jobs and soft delete removed jobs)
        var result = await jobRegistry.ReconcileJobDefinitionsAsync(jobDefinitions, cancellationToken);

        // Log reconciliation summary
        logger.LogInformation(
            "Job reconciliation completed: {AddedCount} job(s) added, {DeletedCount} job(s) soft deleted, {UnchangedCount} job(s) unchanged",
            result.AddedCount,
            result.DeletedCount,
            jobDefinitions.Count - result.AddedCount);

        // Log deleted jobs if any
        if (result.DeletedCount > 0)
        {
            foreach (var deletedKey in result.DeletedJobKeys)
            {
                logger.LogWarning("[Deleted] Job: {JobKey} - No longer exists in code", deletedKey);
            }
        }

        // Register all current job definitions
        foreach (var definition in jobDefinitions)
        {
            var status = result.AddedJobKeys.Contains(definition.JobKey) ? "Added" : "Already Registered";
            await jobRegistry.RegisterJob(definition, status);
        }

        if (_jobSchedulerOptions.RecurringJobDebugMode)
        {
            logger.LogDebug("Received JobDefinitionsChangedEvent in debug mode, skipping schedule updates");
            return;
        }

        // Publish JobDefinitionsChangedEvent to notify subscribers
        try
        {
            if (result is { AddedCount: 0, DeletedCount: 0 })
            {
                logger.LogInformation("No job definitions changed, skipping event publication");
                return;
            }

            await eventBus.PublishAsync(new JobDefinitionsChangedEvent
            {
                FromProject = Assembly.GetEntryAssembly()?.GetName().Name!,
                AddedDefinitions = jobDefinitions.Where(d => result.AddedJobKeys.Contains(d.JobKey)).ToList(),
                AddedJobKeys = result.AddedJobKeys.ToList(),
                DeletedJobKeys = result.DeletedJobKeys.ToList(),
                UpdatedDefinitions = [],  // Reconciliation does not handle updates
                UpdatedJobKeys = [],      // Reconciliation does not handle updates
                ReconciledAt = DateTime.UtcNow
            }, cancellationToken: cancellationToken);

            logger.LogInformation("Published JobDefinitionsChangedEvent to notify subscribers of reconciliation");
        }
        catch (Exception eventEx)
        {
            // Log error but don't fail startup if event publishing fails
            logger.LogError(eventEx, "Failed to publish JobDefinitionsChangedEvent, but continuing startup");
        }
    }
}
