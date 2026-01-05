using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MoLibrary.StateStore.MemoryProvider;

/// <summary>
/// Memory cache based state store implementation
/// </summary>
public class MemoryCacheProvider(IMemoryCache memoryCache, ILogger<MemoryCacheProvider> logger)
    : StateStoreBase(logger), IMemoryStateStore
{
    private readonly ConcurrentDictionary<string, byte> _keyRegistry = new();

    public override Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removeEmptyValue = true, CancellationToken cancellationToken = default) where T : default
    {
        var result = new Dictionary<string, T?>();

        foreach (var key in keys)
        {
            if (memoryCache.TryGetValue(key, out var entry) && entry is StateEntry<T> stateEntry)
            {
                if (!removeEmptyValue || stateEntry.Value != null)
                {
                    result[key] = stateEntry.Value;
                }
            }
            else if (!removeEmptyValue)
            {
                result[key] = default;
            }
        }

        return Task.FromResult(result);
    }

    public override Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default) where T : default
    {
        if (memoryCache.TryGetValue(key, out var entry) && entry is StateEntry<T> stateEntry)
        {
            return Task.FromResult(stateEntry.Value);
        }

        return Task.FromResult(default(T));
    }

    public override Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        // Register key
        _keyRegistry.TryAdd(key, 0);

        StateEntry<T> stateEntry;

        if (memoryCache.TryGetValue(key, out var existingEntry) && existingEntry is StateEntry<T> existing)
        {
            // Update existing entry
            existing.Update(value);
            stateEntry = existing;
        }
        else
        {
            // Create new entry
            stateEntry = new StateEntry<T>(value, 0);
        }

        var options = new MemoryCacheEntryOptions();
        if (ttl.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = ttl.Value;
        }

        memoryCache.Set(key, stateEntry, options);
        Logger.LogDebug("Saved state with key: {Key}", key);
        return Task.CompletedTask;
    }

    public override Task DeleteStateAsync(string key, CancellationToken cancellationToken = default)
    {
        // Remove key from registry
        _keyRegistry.TryRemove(key, out _);

        memoryCache.Remove(key);

        Logger.LogDebug("Deleted state with key: {Key}", key);
        return Task.CompletedTask;
    }

    public override Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            // Remove key from registry
            _keyRegistry.TryRemove(key, out _);

            memoryCache.Remove(key);
        }

        Logger.LogDebug("Deleted {Count} states", keys.Count);
        return Task.CompletedTask;
    }

    public override Task<(T? Value, string ETag)> GetStateAndVersionAsync<T>(string key,
        CancellationToken cancellationToken = default) where T : default
    {
        if (memoryCache.TryGetValue(key, out var entry) && entry is StateEntry<T> stateEntry)
        {
            return Task.FromResult<(T?, string)>((stateEntry.Value, stateEntry.ETag));
        }

        return Task.FromResult<(T?, string)>((default(T), ""));
    }

    public override Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(string key, T value, string expectedETag,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        // Use lock for atomic operation
        lock (memoryCache)
        {
            if (memoryCache.TryGetValue(key, out var entry) && entry is StateEntry<T> existing)
            {
                // Verify ETag matches
                if (existing.ETag != expectedETag)
                {
                    Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}, Actual: {Actual}",
                        key, expectedETag, existing.ETag);
                    return Task.FromResult<(bool, string?)>((false, null));
                }

                // ETag matches, update entry
                existing.Update(value);

                var options = new MemoryCacheEntryOptions();
                if (ttl.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = ttl.Value;
                }

                memoryCache.Set(key, existing, options);
                Logger.LogDebug("Updated state with ETag verification for key: {Key}, NewETag: {ETag}", key, existing.ETag);
                return Task.FromResult<(bool, string?)>((true, existing.ETag));
            }

            // Key doesn't exist, ETag verification failed
            Logger.LogDebug("Key not found for ETag verification: {Key}", key);
            return Task.FromResult<(bool, string?)>((false, null));
        }
    }

    public override Task<bool> TrySaveStateIfNotExistsAsync<T>(string key, T value,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        // Use lock for atomic operation
        lock (memoryCache)
        {
            if (memoryCache.TryGetValue(key, out _))
            {
                // Key already exists, return failure
                Logger.LogDebug("Key already exists, cannot save: {Key}", key);
                return Task.FromResult(false);
            }

            // Key doesn't exist, create new entry
            var stateEntry = new StateEntry<T>(value, 0);

            var options = new MemoryCacheEntryOptions();
            if (ttl.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = ttl.Value;
            }

            // Register key
            _keyRegistry.TryAdd(key, 0);

            memoryCache.Set(key, stateEntry, options);
            Logger.LogDebug("Saved state (if not exists) with key: {Key}", key);
            return Task.FromResult(true);
        }
    }

    public override Task<List<string>> ScanKeysAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var regex = GlobToRegex(pattern);
        var keys = _keyRegistry.Keys
            .Where(k => regex.IsMatch(k))
            .ToList();

        Logger.LogDebug("Found {Count} keys matching pattern: {Pattern}", keys.Count, pattern);
        return Task.FromResult(keys);
    }

    /// <summary>
    /// Convert glob pattern to regex
    /// </summary>
    private static Regex GlobToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return new Regex(regexPattern, RegexOptions.Compiled);
    }
}
