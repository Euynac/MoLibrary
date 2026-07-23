using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.StateStore.Abstractions;

namespace Monica.JobScheduler.Services;

/// <summary>
/// High-performance in-memory cache layer for JobDefinition management.
/// Inherits from JobDefinitionCacheServiceDefault to reuse event publishing logic.
/// Provides write-through caching with lazy invalidation based on JobDefinitionsChangedEvent.
/// Thread-safe for concurrent access using per-job locks for high concurrency.
/// </summary>
public class JobDefinitionCacheService : JobDefinitionCacheServiceDefault, IDisposable, IAsyncDisposable
{
    private readonly IStateStore _staleStore;
    private readonly string _stalePrefix;

    private readonly ConcurrentDictionary<string, JobDefinition> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _jobLocks = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly IAsyncDisposable? _eventSubscription;

    private static readonly TimeSpan _staleFlagTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);

    public JobDefinitionCacheService(
        IJobMetadataRepository metadataRepository,
        [FromKeyedServices(nameof(ModuleJobScheduler))] IStateStore stateStore,
        [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
        IOptions<ModuleJobSchedulerOption> options,
        ILogger<JobDefinitionCacheService> logger)
        : base(metadataRepository, eventBus, options, logger)
    {
        _staleStore = stateStore;
        _stalePrefix = $"JobDefinition:Updated:{JobSchedulerOptions.SchedulerScopeKey}:";

        // Subscribe to JobDefinitionsChangedEvent for cache invalidation
        _eventSubscription = eventBus.SubscribeAsync<JobDefinitionsChangedEvent>(
                OnJobDefinitionsChangedAsync,
                JobEventTopicHelper.GetTopicName<JobDefinitionsChangedEvent>(JobSchedulerOptions.SchedulerScopeKey))
            .GetAwaiter().GetResult();
        Logger.LogDebug("JobDefinitionCacheService initialized and subscribed to JobDefinitionsChangedEvent");
    }

    /// <inheritdoc />
    public override async Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty", nameof(jobKey));
        }

        var jobLock = _jobLocks.GetOrAdd(jobKey, _ => new SemaphoreSlim(1, 1));

        if (!await jobLock.WaitAsync(_lockTimeout, cancellationToken))
        {
            Logger.LogError("Timeout acquiring lock for job {JobKey}", jobKey);
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
                    isStale = await _staleStore.ExistAsync(_stalePrefix + jobKey, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // StateStore failure: log warning, assume not stale (availability over consistency)
                    Logger.LogWarning(ex, "Failed to check staleness for job {JobKey}, assuming fresh", jobKey);
                }

                if (isStale)
                {
                    Logger.LogDebug("Cache stale for job {JobKey}, reloading from metadata store", jobKey);

                    // Reload from metadata store
                    var reloadedDefinition = await MetadataRepository.GetDefinitionAsync(jobKey, cancellationToken);

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
                        await _staleStore.DeleteStateAsync(_stalePrefix + jobKey, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to clear staleness flag for job {JobKey}", jobKey);
                    }

                    return reloadedDefinition;
                }

                Logger.LogDebug("Cache hit for job {JobKey}", jobKey);
                return cachedDefinition;
            }

            // Cache miss: load from metadata store
            Logger.LogDebug("Cache miss for job {JobKey}, loading from metadata store", jobKey);
            var definition = await MetadataRepository.GetDefinitionAsync(jobKey, cancellationToken);

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
    public override async Task<IReadOnlyList<JobDefinition>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        // If cache is empty, initialize it
        if (_cache.IsEmpty)
        {
            if (!await _initLock.WaitAsync(_lockTimeout, cancellationToken))
            {
                Logger.LogError("Timeout acquiring initialization lock");
                throw new TimeoutException("Timeout acquiring initialization lock");
            }

            try
            {
                // Double-check after acquiring lock (race condition protection)
                if (_cache.IsEmpty)
                {
                    Logger.LogInformation("Initializing cache from metadata store");
                    var query = new JobDefinitionQuery
                    {
                        IncludeDeleted = false,
                        PageNumber = 1,
                        PageSize = int.MaxValue // 获取所有结果
                    };
                    var result = await MetadataRepository.QueryDefinitionsAsync(query, cancellationToken);

                    foreach (var definition in result.Items)
                    {
                        _cache[definition.JobKey] = definition;
                    }

                    Logger.LogInformation("Cache initialized with {Count} job definitions", result.TotalCount);
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
    public override async Task SaveDefinitionAsync(
        JobDefinition definition,
        bool publishChangeEvent = false,
        CancellationToken cancellationToken = default)
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
            Logger.LogError("Timeout acquiring lock for job {JobKey}", definition.JobKey);
            throw new TimeoutException($"Timeout acquiring lock for job {definition.JobKey}");
        }

        try
        {
            // Write-through: persist to metadata store first (calls base method with event publishing)
            await base.SaveDefinitionAsync(definition, publishChangeEvent, cancellationToken);

            // Update cache
            _cache[definition.JobKey] = definition;

            // Mark as stale in StateStore for distributed scenarios
            try
            {
                await _staleStore.SaveStateAsync(
                    _stalePrefix + definition.JobKey,
                    true,
                    cancellationToken,
                    _staleFlagTtl);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to mark job {JobKey} as stale in StateStore", definition.JobKey);
            }

            Logger.LogDebug("Saved job definition to cache and metadata store: {JobKey}", definition.JobKey);
        }
        finally
        {
            jobLock.Release();
        }
    }

    /// <summary>
    /// Handles JobDefinitionsChangedEvent to invalidate cache entries.
    /// </summary>
    private async Task OnJobDefinitionsChangedAsync(
        JobDefinitionsChangedEvent evt,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(evt.SchedulerScopeKey, JobSchedulerOptions.SchedulerScopeKey, StringComparison.Ordinal))
        {
            Logger.LogDebug("Ignored JobDefinitionsChangedEvent for foreign scope {Scope}", evt.SchedulerScopeKey);
            return;
        }

        Logger.LogInformation(
            "Processing JobDefinitionsChangedEvent: {AddedCount} added, {UpdatedCount} updated, {DeletedCount} deleted",
            evt.AddedJobKeys.Count,
            evt.UpdatedJobKeys.Count,
            evt.DeletedJobKeys.Count);

        // Mark added/updated jobs as stale (lazy invalidation)
        foreach (var jobKey in evt.AddedJobKeys.Concat(evt.UpdatedJobKeys))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _staleStore.SaveStateAsync(
                    _stalePrefix + jobKey,
                    true,
                    cancellationToken,
                    ttl: _staleFlagTtl);

                Logger.LogDebug("Marked job {JobKey} as stale", jobKey);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to mark job {JobKey} as stale", jobKey);
            }
        }

        // Remove deleted jobs from cache immediately
        foreach (var jobKey in evt.DeletedJobKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _cache.TryRemove(jobKey, out _);
            Logger.LogDebug("Removed deleted job {JobKey} from cache", jobKey);

            try
            {
                await _staleStore.DeleteStateAsync(_stalePrefix + jobKey, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to delete stale flag for deleted job {JobKey}", jobKey);
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

        Logger.LogDebug("JobDefinitionCacheService disposed");
    }
}
