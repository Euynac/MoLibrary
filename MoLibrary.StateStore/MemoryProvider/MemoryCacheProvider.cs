using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MoLibrary.StateStore.MemoryProvider;

/// <summary>
/// 基于 MemoryCache 的内存状态存储提供者
/// </summary>
public class MemoryCacheProvider : IMemoryStateStore, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheProvider>? _logger;
    private readonly ConcurrentDictionary<string, string> _etagStore;
    private bool _disposed;

    public MemoryCacheProvider(IMemoryCache memoryCache, ILogger<MemoryCacheProvider>? logger = null)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger;
        _etagStore = new ConcurrentDictionary<string, string>();
    }

    /// <summary>
    /// 生成完整的键，包含前缀
    /// </summary>
    private string GenerateFullKey<T>(string key, string? prefix = null)
    {
        var actualPrefix = prefix ?? typeof(T).Name;
        return $"{actualPrefix}:{key}";
    }

    /// <summary>
    /// 生成完整的键，使用指定前缀
    /// </summary>
    private string GenerateFullKey(string key, string? prefix = null)
    {
        return string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
    }

    /// <summary>
    /// 生成 ETag
    /// </summary>
    private string GenerateETag()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>
    /// 序列化对象到 JSON
    /// </summary>
    private string? SerializeValue<T>(T value)
    {
        if (value == null) return null;
        if (value is string str) return str;
        return JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 从 JSON 反序列化对象
    /// </summary>
    private T? DeserializeValue<T>(string? json)
    {
        if (string.IsNullOrEmpty(json)) return default;
        if (typeof(T) == typeof(string)) return (T)(object)json;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "反序列化失败，返回默认值");
            return default;
        }
    }

    public Task<bool> ExistAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey<T>(key);
        var exists = _memoryCache.TryGetValue(fullKey, out _);
        return Task.FromResult(exists);
    }

    public Task<bool> ExistAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey<T>(key, prefix);
        var exists = _memoryCache.TryGetValue(fullKey, out _);
        return Task.FromResult(exists);
    }

    public Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, bool removePrefix = true, bool removeEmptyValue = true, CancellationToken cancellationToken = default)
    {
        return GetBulkStateAsync<T>(keys, null, removePrefix, removeEmptyValue, cancellationToken);
    }

    public Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix, bool removePrefix = true, bool removeEmptyValue = true, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, T?>();
        var actualPrefix = prefix ?? typeof(T).Name;

        foreach (var key in keys)
        {
            var fullKey = GenerateFullKey<T>(key, prefix);
            if (_memoryCache.TryGetValue(fullKey, out var cachedValue))
            {
                var deserializedValue = DeserializeValue<T>(cachedValue?.ToString());
                if (!removeEmptyValue || deserializedValue != null)
                {
                    var resultKey = removePrefix ? key : fullKey;
                    result[resultKey] = deserializedValue;
                }
            }
        }

        return Task.FromResult(result);
    }

    public Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, bool removePrefix = true, bool removeEmptyValue = true, CancellationToken cancellationToken = default)
    {
        return GetBulkStateAsync(keys, null, removePrefix, removeEmptyValue, cancellationToken);
    }

    public Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix, bool removePrefix = true, bool removeEmptyValue = true, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();

        foreach (var key in keys)
        {
            var fullKey = GenerateFullKey(key, prefix);
            if (_memoryCache.TryGetValue(fullKey, out var cachedValue))
            {
                var stringValue = cachedValue?.ToString();
                if (!removeEmptyValue || !string.IsNullOrEmpty(stringValue))
                {
                    var resultKey = removePrefix ? key : fullKey;
                    result[resultKey] = stringValue ?? string.Empty;
                }
            }
        }

        return Task.FromResult(result);
    }

    public Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey<T>(key);
        if (_memoryCache.TryGetValue(fullKey, out var cachedValue))
        {
            return Task.FromResult(DeserializeValue<T>(cachedValue?.ToString()));
        }
        return Task.FromResult<T?>(default);
    }

    public Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey<T>(key, prefix);
        if (_memoryCache.TryGetValue(fullKey, out var cachedValue))
        {
            return Task.FromResult(DeserializeValue<T>(cachedValue?.ToString()));
        }
        return Task.FromResult<T?>(default);
    }

    public Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out var cachedValue))
        {
            return Task.FromResult(cachedValue?.ToString());
        }
        return Task.FromResult<string?>(null);
    }

    public Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey(key, prefix);
        if (_memoryCache.TryGetValue(fullKey, out var cachedValue))
        {
            return Task.FromResult(cachedValue?.ToString());
        }
        return Task.FromResult<string?>(null);
    }

    public Task<T?> GetSingleStateAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        var key = typeof(T).Name;
        if (_memoryCache.TryGetValue(key, out var cachedValue))
        {
            return Task.FromResult(DeserializeValue<T>(cachedValue?.ToString()));
        }
        return Task.FromResult<T?>(null);
    }

    public Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var fullKey = GenerateFullKey<T>(key);
        var serializedValue = SerializeValue(value);
        
        var options = new MemoryCacheEntryOptions();
        if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
        {
            options.AbsoluteExpirationRelativeToNow = ttl.Value;
        }

        _memoryCache.Set(fullKey, serializedValue, options);
        
        // 更新 ETag
        var etag = GenerateETag();
        _etagStore.AddOrUpdate(fullKey, etag, (k, v) => etag);

        return Task.CompletedTask;
    }

    public Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var fullKey = GenerateFullKey<T>(key, prefix);
        var serializedValue = SerializeValue(value);
        
        var options = new MemoryCacheEntryOptions();
        if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
        {
            options.AbsoluteExpirationRelativeToNow = ttl.Value;
        }

        _memoryCache.Set(fullKey, serializedValue, options);
        
        // 更新 ETag
        var etag = GenerateETag();
        _etagStore.AddOrUpdate(fullKey, etag, (k, v) => etag);

        return Task.CompletedTask;
    }

    public Task SaveSingleStateAsync<T>(T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null) where T : class
    {
        var key = typeof(T).Name;
        var serializedValue = SerializeValue(value);
        
        var options = new MemoryCacheEntryOptions();
        if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
        {
            options.AbsoluteExpirationRelativeToNow = ttl.Value;
        }

        _memoryCache.Set(key, serializedValue, options);
        
        // 更新 ETag
        var etag = GenerateETag();
        _etagStore.AddOrUpdate(key, etag, (k, v) => etag);

        return Task.CompletedTask;
    }

    public Task DeleteStateAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        _etagStore.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey(key, prefix);
        _memoryCache.Remove(fullKey);
        _etagStore.TryRemove(fullKey, out _);
        return Task.CompletedTask;
    }

    public Task DeleteSingleStateAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        var key = typeof(T).Name;
        _memoryCache.Remove(key);
        _etagStore.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            _memoryCache.Remove(key);
            _etagStore.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    public Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var fullKey = GenerateFullKey(key, prefix);
            _memoryCache.Remove(fullKey);
            _etagStore.TryRemove(fullKey, out _);
        }
        return Task.CompletedTask;
    }

    public Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey<T>(key);
        if (_memoryCache.TryGetValue(fullKey, out var cachedValue))
        {
            var deserializedValue = DeserializeValue<T>(cachedValue?.ToString());
            var etag = _etagStore.GetOrAdd(fullKey, _ => GenerateETag());
            return Task.FromResult((deserializedValue!, etag));
        }
        throw new KeyNotFoundException($"状态键 '{fullKey}' 不存在");
    }

    public Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var fullKey = GenerateFullKey<T>(key, prefix);
        if (_memoryCache.TryGetValue(fullKey, out var cachedValue))
        {
            var deserializedValue = DeserializeValue<T>(cachedValue?.ToString());
            var etag = _etagStore.GetOrAdd(fullKey, _ => GenerateETag());
            return Task.FromResult((deserializedValue!, etag));
        }
        throw new KeyNotFoundException($"状态键 '{fullKey}' 不存在");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _etagStore.Clear();
            _disposed = true;
        }
    }
} 