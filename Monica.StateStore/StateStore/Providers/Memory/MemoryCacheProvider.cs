using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Monica.StateStore.StateStore.Abstractions;
using Monica.StateStore.StateStore.Models;

namespace Monica.StateStore.StateStore.Providers.Memory;

/// <summary>
/// Memory cache based state store implementation
/// </summary>
public class MemoryCacheProvider(IMemoryCache memoryCache, ILogger<MemoryCacheProvider> logger)
    : StateStoreBase(logger), IMemoryStateStore, IStateStoreKeyTtlReader
{
    private readonly ConcurrentDictionary<string, byte> _keyRegistry = new();

    public override Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removeEmptyValue = true, CancellationToken cancellationToken = default) where T : default
    {
        var result = new Dictionary<string, T?>();

        foreach (var key in keys)
        {
            if (memoryCache.TryGetValue(key, out var entry) && entry is IStateEntry {Value: T value})
            {
                result[key] = value;
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
        if (memoryCache.TryGetValue(key, out var entry) && entry is IStateEntry {Value: T value})
        {
            return Task.FromResult<T?>(value);
        }

        return Task.FromResult<T?>(default);
    }

    public override Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        // Register key
        _keyRegistry.TryAdd(key, 0);

        var expiresAt = ResolveExpiration(ttl);

        StateEntry<T> stateEntry;

        if (memoryCache.TryGetValue(key, out var existingEntry) && existingEntry is StateEntry<T> existing)
        {
            // Update existing entry
            existing.Update(value, expiresAt);
            stateEntry = existing;
        }
        else
        {
            // Create new entry
            stateEntry = new StateEntry<T>(value, 0, expiresAt);
        }

        memoryCache.Set(key, stateEntry, CreateEntryOptions(expiresAt));
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

    public override Task<(T? Value, string ETag)> GetStateAndETagAsync<T>(string key,
        CancellationToken cancellationToken = default) where T : default
    {
        if (memoryCache.TryGetValue(key, out var entry) && entry is IStateEntry {Value: T value} stateEntry)
        {
            return Task.FromResult<(T?, string)>((value, stateEntry.ETag));
        }

        return Task.FromResult<(T?, string)>((default(T), ""));
    }


    public override Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(string key, T value, string expectedETag,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var expiresAt = ResolveExpiration(ttl);

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
                existing.Update(value, expiresAt);
                memoryCache.Set(key, existing, CreateEntryOptions(expiresAt));
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
        var expiresAt = ResolveExpiration(ttl);

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
            var stateEntry = new StateEntry<T>(value, 0, expiresAt);

            // Register key
            _keyRegistry.TryAdd(key, 0);

            memoryCache.Set(key, stateEntry, CreateEntryOptions(expiresAt));
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

    public override Task SaveBulkStateAsync<T>(
        IReadOnlyList<(string Key, T Value)> items,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        var expiresAt = ResolveExpiration(ttl);

        foreach (var (key, value) in items)
        {
            // Register key
            _keyRegistry.TryAdd(key, 0);

            StateEntry<T> stateEntry;

            if (memoryCache.TryGetValue(key, out var existingEntry) && existingEntry is StateEntry<T> existing)
            {
                existing.Update(value, expiresAt);
                stateEntry = existing;
            }
            else
            {
                stateEntry = new StateEntry<T>(value, 0, expiresAt);
            }

            memoryCache.Set(key, stateEntry, CreateEntryOptions(expiresAt));
        }

        Logger.LogDebug("Saved {Count} states in bulk", items.Count);
        return Task.CompletedTask;
    }

    public Task<StateStoreKeyTtlSnapshot> GetKeyTtlAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!memoryCache.TryGetValue(key, out var entry) || entry is not IStateEntry stateEntry)
        {
            throw new KeyNotFoundException($"Key not found: {key}");
        }

        return Task.FromResult(CreateTtlSnapshot(stateEntry.ExpiresAt));
    }

    public override Task<bool> TryDeleteStateWithETagAsync(
        string key,
        string expectedETag,
        CancellationToken cancellationToken = default)
    {
        lock (memoryCache)
        {
            if (memoryCache.TryGetValue(key, out var entry) && entry is IStateEntry stateEntry)
            {
                if (stateEntry.ETag != expectedETag)
                {
                    Logger.LogDebug("ETag mismatch for delete, key: {Key}. Expected: {Expected}, Actual: {Actual}",
                        key, expectedETag, stateEntry.ETag);
                    return Task.FromResult(false);
                }

                // ETag matches, delete the key
                _keyRegistry.TryRemove(key, out _);
                memoryCache.Remove(key);
                Logger.LogDebug("Deleted state with ETag for key: {Key}", key);
                return Task.FromResult(true);
            }

            // Key doesn't exist, allow delete if ETag is "0" or empty
            if (expectedETag == "0" || string.IsNullOrEmpty(expectedETag))
            {
                return Task.FromResult(true);
            }

            Logger.LogDebug("Key not found for ETag delete: {Key}", key);
            return Task.FromResult(false);
        }
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

    private static DateTimeOffset? ResolveExpiration(TimeSpan? ttl)
    {
        return ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : null;
    }

    private static MemoryCacheEntryOptions CreateEntryOptions(DateTimeOffset? expiresAt)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiresAt.HasValue)
        {
            options.AbsoluteExpiration = expiresAt;
        }

        return options;
    }

    private static StateStoreKeyTtlSnapshot CreateTtlSnapshot(DateTimeOffset? expiresAt)
    {
        return expiresAt.HasValue
            ? StateStoreKeyTtlSnapshot.FromRemaining(expiresAt.Value - DateTimeOffset.UtcNow)
            : StateStoreKeyTtlSnapshot.Permanent;
    }
}
