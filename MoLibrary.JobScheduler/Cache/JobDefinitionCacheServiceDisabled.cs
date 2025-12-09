using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Cache;

/// <summary>
/// Disabled (pass-through) implementation of IJobDefinitionCacheService.
/// Directly accesses IMoJobMetadataRepository without any caching.
/// Use this implementation when caching is not desired or for debugging purposes.
/// </summary>
public class JobDefinitionCacheServiceDisabled(
    IMoJobMetadataRepository metadataRepository,
    ILogger<JobDefinitionCacheServiceDisabled> logger) : IJobDefinitionCacheService
{
    public async Task<JobDefinition?> GetJobDefinitionAsync(string jobKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(jobKey));
        }

        return await metadataRepository.GetDefinitionAsync(jobKey, cancellationToken);
    }
    
    public async Task<IReadOnlyList<JobDefinition>> GetAllJobDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var query = new JobDefinitionQuery
        {
            IncludeDeleted = false,
            PageNumber = 1,
            PageSize = int.MaxValue
        };
        var result = await metadataRepository.QueryDefinitionsAsync(query, cancellationToken);
        return result.Items;
    }
    
    public async Task SaveJobDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(definition.JobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(definition));
        }

        await metadataRepository.SaveDefinitionAsync(definition, cancellationToken);
    }
}