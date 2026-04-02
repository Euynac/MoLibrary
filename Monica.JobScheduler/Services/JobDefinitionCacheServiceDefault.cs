using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Default (pass-through) implementation of IJobDefinitionCacheService.
/// Directly accesses IJobMetadataRepository without any caching.
/// Serves as the base class for cached implementations, providing event publishing logic.
/// Can be used directly when caching is not desired or for debugging purposes.
/// </summary>
public class JobDefinitionCacheServiceDefault(
    IJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobDefinitionCacheServiceDefault> logger) : IJobDefinitionCacheService
{
    protected readonly ModuleJobSchedulerOption JobSchedulerOptions = options.Value;
    protected readonly IJobMetadataRepository MetadataRepository = metadataRepository;
    protected readonly IEventBus EventBus = eventBus;
    protected readonly ILogger Logger = logger;

    public virtual async Task<JobDefinition?> GetDefinitionAsync(string jobKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(jobKey));
        }

        return await MetadataRepository.GetDefinitionAsync(jobKey, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<JobDefinition>> GetAllDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var query = new JobDefinitionQuery
        {
            IncludeDeleted = false,
            PageNumber = 1,
            PageSize = int.MaxValue
        };
        var result = await MetadataRepository.QueryDefinitionsAsync(query, cancellationToken);
        return result.Items;
    }

    public virtual async Task SaveDefinitionAsync(
        JobDefinition definition,
        bool publishChangeEvent = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(definition.JobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(definition));
        }

        // Save to metadata repository
        await MetadataRepository.SaveDefinitionAsync(definition, cancellationToken);

        // Publish update event if requested
        if (publishChangeEvent)
        {
            await PublishJobUpdatedEventAsync(definition, cancellationToken);
        }
    }

    /// <summary>
    /// Publishes JobDefinitionsChangedEvent for an updated job definition.
    /// Protected virtual to allow subclasses to override if needed.
    /// </summary>
    protected virtual async Task PublishJobUpdatedEventAsync(
        JobDefinition definition,
        CancellationToken cancellationToken)
    {
        try
        {
            await EventBus.PublishAsync(new JobDefinitionsChangedEvent
            {
                SchedulerScopeKey = definition.SchedulerScopeKey,
                FromProject = definition.FromProject,
                AddedDefinitions = [],
                AddedJobKeys = [],
                DeletedJobKeys = [],
                UpdatedDefinitions = [definition],
                UpdatedJobKeys = [definition.JobKey],
                ReconciledAt = DateTime.UtcNow
            },
            JobEventTopicHelper.GetTopicName<JobDefinitionsChangedEvent>(JobSchedulerOptions.SchedulerScopeKey),
            cancellationToken);

            Logger.LogInformation(
                "Published JobDefinitionsChangedEvent for updated job: {JobKey}",
                definition.JobKey);
        }
        catch (Exception ex)
        {
            // Event publishing failure should not block the save operation (data is already persisted)
            Logger.LogWarning(ex,
                "Failed to publish JobDefinitionsChangedEvent for job {JobKey}. " +
                "Job update was saved successfully but some instances may not receive the update immediately.",
                definition.JobKey);
        }
    }
}
