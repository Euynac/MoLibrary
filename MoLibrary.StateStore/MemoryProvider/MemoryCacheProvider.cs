using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MoLibrary.StateStore.MemoryProvider;

/// <summary>
/// 基于MemoryCache的状态存储实现
/// </summary>
public class MemoryCacheProvider(IMemoryCache memoryCache, ILogger<MemoryCacheProvider> logger)
    : StateStoreBase(logger), IMemoryStateStore
{
    private readonly ConcurrentDictionary<string, byte> _keyRegistry = new();

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

        // 注册 key
        _keyRegistry.TryAdd(fullKey, 0);

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

        // 移除 key 注册
        _keyRegistry.TryRemove(fullKey, out _);

        memoryCache.Remove(fullKey);

        Logger.LogDebug("Deleted state with key: {Key}", fullKey);
        return Task.CompletedTask;
    }

    public override Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var fullKey = GetKey(key, prefix);

            // 移除 key 注册
            _keyRegistry.TryRemove(fullKey, out _);

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

    public override Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(string key, T value, string expectedETag,
        string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var fullKey = GetKey(key, prefix);

        // 使用锁确保原子性操作
        lock (memoryCache)
        {
            if (memoryCache.TryGetValue(fullKey, out var entry) && entry is StateEntry<T> existing)
            {
                // 验证 ETag 是否匹配
                if (existing.ETag != expectedETag)
                {
                    Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}, Actual: {Actual}",
                        fullKey, expectedETag, existing.ETag);
                    return Task.FromResult<(bool, string?)>((false, null));
                }

                // ETag 匹配，更新条目
                existing.Update(value);

                var options = new MemoryCacheEntryOptions();
                if (ttl.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = ttl.Value;
                }

                memoryCache.Set(fullKey, existing, options);
                Logger.LogDebug("Updated state with ETag verification for key: {Key}, NewETag: {ETag}", fullKey, existing.ETag);
                return Task.FromResult<(bool, string?)>((true, existing.ETag));
            }

            // Key 不存在，ETag 验证失败
            Logger.LogDebug("Key not found for ETag verification: {Key}", fullKey);
            return Task.FromResult<(bool, string?)>((false, null));
        }
    }

    public override Task<bool> TrySaveStateIfNotExistsAsync<T>(string key, T value, string? prefix,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var fullKey = GetKey(key, prefix);

        // 使用锁确保原子性操作
        lock (memoryCache)
        {
            if (memoryCache.TryGetValue(fullKey, out _))
            {
                // Key 已存在，返回失败
                Logger.LogDebug("Key already exists, cannot save: {Key}", fullKey);
                return Task.FromResult(false);
            }

            // Key 不存在，创建新条目
            var stateEntry = new StateEntry<T>(value, 0);

            var options = new MemoryCacheEntryOptions();
            if (ttl.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = ttl.Value;
            }

            // 注册 key
            _keyRegistry.TryAdd(fullKey, 0);

            memoryCache.Set(fullKey, stateEntry, options);
            Logger.LogDebug("Saved state (if not exists) with key: {Key}", fullKey);
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
    /// 将 glob pattern 转换为正则表达式
    /// </summary>
    private static Regex GlobToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return new Regex(regexPattern, RegexOptions.Compiled);
    }
} 