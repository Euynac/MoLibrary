using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Cache;

/// <summary>
/// Disabled (pass-through) implementation of IJobDefinitionCacheService.
/// Directly accesses IMoJobScheduleMetadataStore without any caching.
/// Use this implementation when caching is not desired or for debugging purposes.
/// </summary>
public class JobDefinitionCacheServiceDisabled(
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobDefinitionCacheServiceDisabled> logger) : IJobDefinitionCacheService
{
    public async Task<JobDefinition?> GetJobDefinitionAsync(string jobKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(jobKey));
        }

        return await metadataStore.GetJobDefinitionAsync(jobKey, cancellationToken);
    }
    
    public async Task<IReadOnlyList<JobDefinition>> GetAllJobDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = await metadataStore.GetAllJobDefinitionsAsync(includeDeleted: false, cancellationToken);
        return definitions;
    }
    
    public async Task SaveJobDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(definition.JobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(definition));
        }

        await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);
    }
}