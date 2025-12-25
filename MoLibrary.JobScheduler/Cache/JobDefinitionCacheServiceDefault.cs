using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;

namespace MoLibrary.JobScheduler.Cache;

/// <summary>
/// Default (pass-through) implementation of IJobDefinitionCacheService.
/// Directly accesses IMoJobMetadataRepository without any caching.
/// Serves as the base class for cached implementations, providing event publishing logic.
/// Can be used directly when caching is not desired or for debugging purposes.
/// </summary>
public class JobDefinitionCacheServiceDefault(
    IMoJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    ILogger<JobDefinitionCacheServiceDefault> logger) : IJobDefinitionCacheService
{
    protected readonly IMoJobMetadataRepository MetadataRepository = metadataRepository;
    protected readonly IMoEventBus EventBus = eventBus;
    protected readonly ILogger Logger = logger;

    public virtual async Task<JobDefinition?> GetJobDefinitionAsync(string jobKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(jobKey));
        }

        return await MetadataRepository.GetDefinitionAsync(jobKey, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<JobDefinition>> GetAllJobDefinitionsAsync(
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

    public virtual async Task SaveJobDefinitionAsync(
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
            var projectName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";
            await EventBus.PublishAsync(new JobDefinitionsChangedEvent
            {
                FromProject = projectName,
                AddedDefinitions = [],
                AddedJobKeys = [],
                DeletedJobKeys = [],
                UpdatedDefinitions = [definition],
                UpdatedJobKeys = [definition.JobKey],
                ReconciledAt = DateTime.UtcNow
            }, cancellationToken: cancellationToken);

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
