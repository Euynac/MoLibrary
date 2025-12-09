using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.StateStore;

namespace MoLibrary.JobScheduler.Cache;

/// <summary>
/// High-performance in-memory cache layer for JobDefinition management.
/// Provides write-through caching with lazy invalidation based on JobDefinitionsChangedEvent.
/// Thread-safe for concurrent access using per-job locks for high concurrency.
/// </summary>
public class JobDefinitionCacheService : IJobDefinitionCacheService, IDisposable, IAsyncDisposable
{
    private readonly IMoJobMetadataRepository _metadataRepository;
    private readonly IMoStateStore _stateStore;
    private readonly ILogger<JobDefinitionCacheService> _logger;

    private readonly ConcurrentDictionary<string, JobDefinition> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _jobLocks = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IAsyncDisposable? _eventSubscription;

    private const string STALE_FLAG_PREFIX = "JobDefinition:Updated";
    private static readonly TimeSpan _staleFlagTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);

    public JobDefinitionCacheService(
        IMoJobMetadataRepository metadataRepository,
        [FromKeyedServices(nameof(ModuleJobScheduler))] IMoStateStore stateStore,
        [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
        ILogger<JobDefinitionCacheService> logger)
    {
        _metadataRepository = metadataRepository;
        _stateStore = stateStore;
        _logger = logger;

        // Subscribe to JobDefinitionsChangedEvent for cache invalidation
        _eventSubscription = eventBus.Subscribe<JobDefinitionsChangedEvent>(OnJobDefinitionsChangedAsync);
        _logger.LogDebug("JobDefinitionCacheService initialized and subscribed to JobDefinitionsChangedEvent");
    }

    /// <inheritdoc />
    public async Task<JobDefinition?> GetJobDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(jobKey));
        }

        var jobLock = _jobLocks.GetOrAdd(jobKey, _ => new SemaphoreSlim(1, 1));

        if (!await jobLock.WaitAsync(_lockTimeout, cancellationToken))
        {
            _logger.LogError("Timeout acquiring lock for job {JobKey}", jobKey);
            throw new TimeoutException($"Timeout acquiring lock for job {jobKey}");
        }

        try
        {
            // Check if cached
            if (_cache.TryGetValue(jobKey, out var cachedDefinition))
            {
                // Check if stale via StateStore
                bool isStale = false;
                try
                {
                    isStale = await _stateStore.ExistAsync<bool>(jobKey, STALE_FLAG_PREFIX, cancellationToken);
                }
                catch (Exception ex)
                {
                    // StateStore failure: log warning, assume not stale (availability over consistency)
                    _logger.LogWarning(ex, "Failed to check staleness for job {JobKey}, assuming fresh", jobKey);
                }

                if (isStale)
                {
                    _logger.LogDebug("Cache stale for job {JobKey}, reloading from metadata store", jobKey);

                    // Reload from metadata store
                    var reloadedDefinition = await _metadataRepository.GetDefinitionAsync(jobKey, cancellationToken);

                    if (reloadedDefinition != null)
                    {
                        _cache[jobKey] = reloadedDefinition;
                    }
                    else
                    {
                        // Job was deleted, remove from cache
                        _cache.TryRemove(jobKey, out _);
                    }

                    // Clear staleness flag
                    try
                    {
                        await _stateStore.DeleteStateAsync(jobKey, STALE_FLAG_PREFIX, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clear staleness flag for job {JobKey}", jobKey);
                    }

                    return reloadedDefinition;
                }

                _logger.LogDebug("Cache hit for job {JobKey}", jobKey);
                return cachedDefinition;
            }

            // Cache miss: load from metadata store
            _logger.LogDebug("Cache miss for job {JobKey}, loading from metadata store", jobKey);
            var definition = await _metadataRepository.GetDefinitionAsync(jobKey, cancellationToken);

            if (definition != null)
            {
                _cache[jobKey] = definition;
            }

            return definition;
        }
        finally
        {
            jobLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobDefinition>> GetAllJobDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        // If cache is empty, initialize it
        if (_cache.IsEmpty)
        {
            if (!await _initLock.WaitAsync(_lockTimeout, cancellationToken))
            {
                _logger.LogError("Timeout acquiring initialization lock");
                throw new TimeoutException("Timeout acquiring initialization lock");
            }

            try
            {
                // Double-check after acquiring lock (race condition protection)
                if (_cache.IsEmpty)
                {
                    _logger.LogInformation("Initializing cache from metadata store");
                    var query = new JobDefinitionQuery
                    {
                        IncludeDeleted = false,
                        PageNumber = 1,
                        PageSize = int.MaxValue // 获取所有结果
                    };
                    var result = await _metadataRepository.QueryDefinitionsAsync(query, cancellationToken);

                    foreach (var definition in result.Items)
                    {
                        _cache[definition.JobKey] = definition;
                    }

                    _logger.LogInformation("Cache initialized with {Count} job definitions", result.TotalCount);
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        return _cache.Values.ToList();
    }

    /// <inheritdoc />
    public async Task SaveJobDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.JobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(definition));
        }

        var jobLock = _jobLocks.GetOrAdd(definition.JobKey, _ => new SemaphoreSlim(1, 1));

        if (!await jobLock.WaitAsync(_lockTimeout, cancellationToken))
        {
            _logger.LogError("Timeout acquiring lock for job {JobKey}", definition.JobKey);
            throw new TimeoutException($"Timeout acquiring lock for job {definition.JobKey}");
        }

        try
        {
            // Write-through: persist to metadata store first
            await _metadataRepository.SaveDefinitionAsync(definition, cancellationToken);

            // Update cache
            _cache[definition.JobKey] = definition;

            // Mark as stale in StateStore for distributed scenarios
            try
            {
                await _stateStore.SaveStateAsync(
                    definition.JobKey,
                    true,
                    STALE_FLAG_PREFIX,
                    cancellationToken,
                    _staleFlagTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark job {JobKey} as stale in StateStore", definition.JobKey);
            }

            _logger.LogInformation("Job definition saved (write-through) for {JobKey}", definition.JobKey);
        }
        finally
        {
            jobLock.Release();
        }
    }

    /// <summary>
    /// Handles JobDefinitionsChangedEvent to invalidate cache entries.
    /// </summary>
    private async Task OnJobDefinitionsChangedAsync(JobDefinitionsChangedEvent evt)
    {
        _logger.LogInformation(
            "Processing JobDefinitionsChangedEvent: {AddedCount} added, {DeletedCount} deleted",
            evt.AddedJobKeys.Count,
            evt.DeletedJobKeys.Count);

        // Mark added/updated jobs as stale (lazy invalidation)
        foreach (var jobKey in evt.AddedJobKeys)
        {
            try
            {
                await _stateStore.SaveStateAsync(
                    jobKey,
                    true,
                    STALE_FLAG_PREFIX,
                    ttl: _staleFlagTtl);

                _logger.LogDebug("Marked job {JobKey} as stale", jobKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark job {JobKey} as stale", jobKey);
            }
        }

        // Remove deleted jobs from cache immediately
        foreach (var jobKey in evt.DeletedJobKeys)
        {
            _cache.TryRemove(jobKey, out _);
            _logger.LogDebug("Removed deleted job {JobKey} from cache", jobKey);

            try
            {
                await _stateStore.DeleteStateAsync(jobKey, STALE_FLAG_PREFIX);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete stale flag for deleted job {JobKey}", jobKey);
            }
        }
    }

    /// <summary>
    /// Disposes event subscription and releases resources synchronously.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes event subscription and releases resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_eventSubscription != null)
        {
            await _eventSubscription.DisposeAsync();
        }
        _initLock.Dispose();

        // Dispose all per-job locks
        foreach (var semaphore in _jobLocks.Values)
        {
            semaphore.Dispose();
        }

        _jobLocks.Clear();
        _cache.Clear();

        _logger.LogDebug("JobDefinitionCacheService disposed");
    }
}
