using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Hosted service that registers discovered job definitions to the JobRegistry during application startup.
/// Ensures all jobs are properly registered before the application begins serving requests.
/// Publishes JobDefinitionsChangedEvent after reconciliation to notify subscribers of changes.
/// </summary>
internal class JobRegistrationHostedService(
    JobRegistry jobRegistry,
    IReadOnlyList<JobDefinition> jobDefinitions,
    ILogger<JobRegistrationHostedService> logger,
    ILeaderService leaderService,
    IMoEventBus eventBus, IOptions<ModuleJobSchedulerOption> option) : IHostedService
{
    /// <summary>
    /// Registers all discovered job definitions when the application starts.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if ((await leaderService.GetCurrentLeaderStatusAsync()).IsFailed(out var error, out var data))
        {
            logger.LogError("Error getting leader status: {Error}", error);
            return;
        }
        if (data.Status != LeaderStatus.Leader)
        {
            logger.LogInformation("Not leader, current Leader status is {Status}, skip job registration", data.Status);
            return;
        }

        if (jobDefinitions.Count == 0)
        {
            logger.LogInformation("No job definitions to register");
            return;
        }

        try
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
            if (option.Value.RecurringJobDebugMode)
            {
                logger.LogDebug("Received JobDefinitionsChangedEvent in debug mode, skipping schedule updates");
                return;
            }

            // Publish JobDefinitionsChangedEvent to notify subscribers
            try
            {
                if (result is { AddedCount: 0, DeletedCount: 0})
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
                    ReconciledAt = DateTime.UtcNow
                });

                logger.LogInformation("Published JobDefinitionsChangedEvent to notify subscribers of reconciliation");
            }
            catch (Exception eventEx)
            {
                // Log error but don't fail startup if event publishing fails
                logger.LogError(eventEx, "Failed to publish JobDefinitionsChangedEvent, but continuing startup");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during job registration");
            throw; // Re-throw to prevent application startup if registration fails
        }
    }

    /// <summary>
    /// No cleanup needed when the application stops.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }
}
