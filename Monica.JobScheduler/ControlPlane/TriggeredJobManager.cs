using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.JobScheduler.ControlPlane;

public class TriggeredJobManager(
    IJobDefinitionCacheService cacheService,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    IMoJobMetadataRepository metadataRepository,
    JobRegistry registry,
    IJobCancellationTokenManager jobCancellationManager,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<TriggeredJobManager> logger) : IMoTriggeredJobManager
{
    public async Task<string> EnqueueAsync<TArgs>(TArgs args, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (delay.HasValue && delay.Value < TimeSpan.Zero)
        {
            throw new ArgumentException("Delay cannot be negative", nameof(delay));
        }

        var argsType = typeof(TArgs);
        var jobArgsKey = argsType.FullName
            ?? throw new InvalidOperationException($"Job args type must have full name");

        var definition = await cacheService.GetDefinitionAsync(registry.GetTriggeredJobClrType(jobArgsKey)?.FullName ?? throw new InvalidOperationException($"Job args type {argsType.GetCleanFullName()} not found in JobRegistry"), cancellationToken);
      
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"No triggered job found for args type {jobArgsKey}");
        }

        if (definition.JobType != JobType.Triggered)
        {
            throw new InvalidOperationException(
                $"Job {definition.JobKey} is not a triggered job");
        }

        if (definition.IsDisabled)
        {
            throw new InvalidOperationException($"Job {definition.JobKey} is disabled");
        }

        // Pre-generate instance ID for immediate return
        var instanceId = Guid.NewGuid().ToString();
        var jobArgs = JsonSerializer.Serialize(args, options.Value.JobArgsSerializerOptions);

        var triggeredEvent = new JobTriggeredEvent
        {
            InstanceId = instanceId,
            JobKey = definition.JobKey,
            JobArgs = jobArgs,
            Delay = delay,
            TriggeredAt = DateTime.UtcNow,
        };

        await eventBus.PublishAsync(triggeredEvent, null, cancellationToken);

        logger.LogInformation(
            "Triggered job enqueued: {JobKey}, InstanceId: {InstanceId}, Delay: {Delay}",
            definition.JobKey,
            instanceId,
            delay?.ToString() ?? "None");

        return instanceId;
    }

    public async Task<bool> CancelScheduledJobAsync(string instanceId)
    {
        var instance = await metadataRepository.GetInstanceAsync(instanceId);

        if (instance == null)
        {
            logger.LogWarning("Instance {InstanceId} not found for cancellation", instanceId);
            return false;
        }

        if (instance.State != JobState.Scheduled)
        {
            logger.LogWarning(
                "Instance {InstanceId} cannot be cancelled in state {State}",
                instanceId,
                instance.State);
            return false;
        }

        // Cancel the distributed cancellation token
        await jobCancellationManager.CancelJobTokenAsync(instanceId);

        // Publish event to notify JobSchedulerHostedService to cancel timer
        await eventBus.PublishAsync(new JobCancellationRequestedEvent
        {
            InstanceId = instanceId,
            JobKey = instance.JobKey,
            RequestedAt = DateTime.UtcNow
        });

        logger.LogInformation("Cancellation requested for scheduled job {InstanceId}", instanceId);
        return true;
    }
}
