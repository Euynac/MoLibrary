using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MoLibrary.StateStore.MemoryProvider;

/// <summary>
/// 基于MemoryCache的状态存储实现
/// </summary>
public class MemoryCacheProvider(IMemoryCache memoryCache, ILogger<MemoryCacheProvider> logger)
    : StateStoreBase(logger), IMemoryStateStore
{
    public override Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true, bool removeEmptyValue = true, CancellationToken cancellationToken = default) where T : default
    {
        var result = new Dictionary<string, T?>();

        foreach (var key in keys)
        {
            var fullKey = GetKey(key, prefix);
            if (memoryCache.TryGetValue(fullKey, out var entry) && entry is StateEntry<T> stateEntry)
            {
                var resultKey = removePrefix ? RemovePrefix(fullKey, prefix) : fullKey;
                
                if (!removeEmptyValue || stateEntry.Value != null)
                {
                    result[resultKey] = stateEntry.Value;
                }
            }
            else if (!removeEmptyValue)
            {
                var resultKey = removePrefix ? key : fullKey;
                result[resultKey] = default(T);
            }
        }

        return Task.FromResult(result);
    }

    public override Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default) where T : default
    {
        var fullKey = GetKey(key, prefix);
        
        if (memoryCache.TryGetValue(fullKey, out var entry) && entry is StateEntry<T> stateEntry)
        {
            return Task.FromResult(stateEntry.Value);
        }

        return Task.FromResult(default(T));
    }

    public override Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var fullKey = GetKey(key, prefix);

        StateEntry<T> stateEntry;

        if (memoryCache.TryGetValue(fullKey, out var existingEntry) && existingEntry is StateEntry<T> existing)
        {
            // 更新现有条目
            existing.Update(value);
            stateEntry = existing;
        }
        else
        {
            // 创建新条目
            stateEntry = new StateEntry<T>(value, 0);
        }

        var options = new MemoryCacheEntryOptions();
        if (ttl.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = ttl.Value;
        }

        memoryCache.Set(fullKey, stateEntry, options);
        Logger.LogDebug("Saved state with key: {Key}", fullKey);
        return Task.CompletedTask;
    }

    public override Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var fullKey = GetKey(key, prefix);
        memoryCache.Remove(fullKey);
        
        Logger.LogDebug("Deleted state with key: {Key}", fullKey);
        return Task.CompletedTask;
    }

    public override Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var fullKey = GetKey(key, prefix);
            memoryCache.Remove(fullKey);
        }
        
        Logger.LogDebug("Deleted {Count} states", keys.Count);
        return Task.CompletedTask;
    }

    public override Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix,
        CancellationToken cancellationToken = default)
    {
        var fullKey = GetKey(key, prefix);
        
        if (memoryCache.TryGetValue(fullKey, out var entry) && entry is StateEntry<T> stateEntry)
        {
            return Task.FromResult((stateEntry.Value!, stateEntry.ETag));
        }

        return Task.FromResult((default(T)!, ""));
    }
} 