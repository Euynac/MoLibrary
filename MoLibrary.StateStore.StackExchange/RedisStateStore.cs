using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;
using MoLibrary.StateStore.StackExchange.Modules;
using MoLibrary.Tool.Extensions;
using StackExchange.Redis;

namespace MoLibrary.StateStore.StackExchange;

/// <summary>
/// Redis 状态存储实现类（基于 StackExchange.Redis）
/// </summary>
public class RedisStateStore : DistributedStateStoreBase
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ModuleRedisStateStoreOption _option;
    private readonly IDatabase _database;

    public RedisStateStore(
        IConnectionMultiplexer connection,
        IOptions<ModuleRedisStateStoreOption> options,
        ILogger<RedisStateStore> logger) : base(logger)
    {
        _connection = connection;
        _option = options.Value;
        _database = _connection.GetDatabase();
    }

    public override async Task<List<string>> GetAllKeysByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{prefix}&&*";
            var keys = new List<string>();
            var server = _connection.GetServers().FirstOrDefault()
                ?? throw new InvalidOperationException("No Redis server available");

            await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(cancellationToken))
            {
                var keyStr = key.ToString();
                // 移除前缀: "prefix&&key" -> "key"
                var cleanKey = RemovePrefix(keyStr, prefix);
                keys.Add(cleanKey);
            }

            Logger.LogDebug("Found {Count} keys with prefix: {Prefix}", keys.Count, prefix);
            return keys;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting all keys by prefix {0}", prefix);
        }
    }

    public override Task<Dictionary<string, T?>> QueryStateAsync<T>(
        Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query,
        CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException(
            "QueryStateAsync is not supported in RedisStateStore. " +
            "Redis does not have native query capabilities. Consider using DaprStateStore with a queryable backend.");
    }

    public override async Task<Dictionary<string, string>> GetBulkStateAsync(
        IReadOnlyList<string> keys,
        string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        var finalKeys = keys.Select(k => (RedisKey)GetKey(k, prefix)).ToArray();
        try
        {
            var values = await _database.StringGetAsync(finalKeys);

            var result = new Dictionary<string, string>();
            for (int i = 0; i < finalKeys.Length; i++)
            {
                var value = values[i];
                if (!value.HasValue && removeEmptyValue)
                    continue;

                var keyStr = finalKeys[i].ToString();
                var cleanKey = removePrefix ? RemovePrefix(keyStr, prefix) : keyStr;
                result[cleanKey] = value.ToString();
            }

            return result;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting bulk state with keys: {0}", string.Join(", ", finalKeys.Select(k => k.ToString())));
        }
    }

    public override async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(
        IReadOnlyList<string> keys,
        string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default) where T : default
    {
        var finalKeys = keys.Select(k => (RedisKey)GetKey(k, prefix)).ToArray();
        try
        {
            var values = await _database.StringGetAsync(finalKeys);

            var result = new Dictionary<string, T?>();
            for (int i = 0; i < finalKeys.Length; i++)
            {
                var value = values[i];
                if (!value.HasValue && removeEmptyValue)
                    continue;

                var keyStr = finalKeys[i].ToString();
                var cleanKey = removePrefix ? RemovePrefix(keyStr, prefix) : keyStr;

                if (value.HasValue)
                {
                    try
                    {
                        var deserialized = JsonSerializer.Deserialize<T>(value.ToString());
                        result[cleanKey] = deserialized;
                    }
                    catch (Exception ex)
                    {
                        throw ex.CreateException(Logger, "Failed to deserialize value for key {0} to type {1}",
                            cleanKey, typeof(T).GetCleanFullName());
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting bulk state with keys: {0}",
                string.Join(", ", finalKeys.Select(k => k.ToString())));
        }
    }

    public override async Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default) where T : default
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var value = await _database.StringGetAsync(finalKey);
            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state with key: {0}", finalKey);
        }
    }

    public override async Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var value = await _database.StringGetAsync(finalKey);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state with key: {0}", finalKey);
        }
    }

    public override async Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);

            await _database.StringSetAsync(finalKey, json, expiry);
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state with key: {0}", finalKey);
        }
    }

    public override async Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            await _database.KeyDeleteAsync(finalKey);
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR deleting state with key: {0}", finalKey);
        }
    }

    public override async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKeys = keys.Select(k => (RedisKey)GetKey(k, prefix)).ToArray();
        try
        {
            await _database.KeyDeleteAsync(finalKeys);
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR deleting bulk state with keys: {0}",
                string.Join(", ", finalKeys.Select(k => k.ToString())));
        }
    }

    public override async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var value = await _database.StringGetAsync(finalKey);
            if (!value.HasValue)
                return (default(T)!, string.Empty);

            var deserialized = JsonSerializer.Deserialize<T>(value.ToString());
            // 使用 hash 作为 ETag
            var etag = value.ToString().GetHashCode().ToString();

            return (deserialized!, etag);
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state and version with key: {0}", finalKey);
        }
    }

    public override async Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(
        string key,
        T value,
        string expectedETag,
        string? prefix,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);

            // 使用 WATCH + MULTI + EXEC 实现乐观锁
            var transaction = _database.CreateTransaction();

            // 检查当前值的 ETag 是否匹配
            var currentValue = await _database.StringGetAsync(finalKey);
            var currentETag = currentValue.HasValue ? currentValue.ToString().GetHashCode().ToString() : string.Empty;

            if (currentETag != expectedETag)
            {
                Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}, Actual: {Actual}",
                    finalKey, expectedETag, currentETag);
                return (false, null);
            }

            // 添加设置操作到事务
            transaction.AddCondition(Condition.StringEqual(finalKey, currentValue));
            _ = transaction.StringSetAsync(finalKey, json, expiry);

            var success = await transaction.ExecuteAsync();

            if (success)
            {
                var newETag = json.GetHashCode().ToString();
                Logger.LogDebug("Saved state with ETag: {ETag} for key: {Key}", newETag, finalKey);
                return (true, newETag);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state with ETag for key: {0}", finalKey);
        }
    }

    public override async Task<bool> TrySaveStateIfNotExistsAsync<T>(
        string key,
        T value,
        string? prefix,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);

            // 使用 SET NX 实现
            var success = await _database.StringSetAsync(finalKey, json, expiry, When.NotExists);

            if (success)
            {
                Logger.LogDebug("Saved state (if not exists) with key: {Key}", finalKey);
            }
            else
            {
                Logger.LogDebug("Key already exists, cannot save: {Key}", finalKey);
            }

            return success;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state if not exists for key: {0}", finalKey);
        }
    }

    /// <summary>
    /// 生成带前缀的键，如果配置了 KeyPrefix，则在最前面添加
    /// </summary>
    protected override string GetKey(string key, string? prefix = null)
    {
        var baseKey = base.GetKey(key, prefix);
        return string.IsNullOrEmpty(_option.KeyPrefix) ? baseKey : $"{_option.KeyPrefix}:{baseKey}";
    }

    /// <summary>
    /// 移除键前缀，包括 KeyPrefix 和业务前缀
    /// </summary>
    protected override string RemovePrefix(string key, string? prefix)
    {
        // 先移除 KeyPrefix
        if (!string.IsNullOrEmpty(_option.KeyPrefix))
        {
            var keyPrefixWithColon = $"{_option.KeyPrefix}:";
            if (key.StartsWith(keyPrefixWithColon))
            {
                key = key.Substring(keyPrefixWithColon.Length);
            }
        }

        // 再移除业务前缀
        return base.RemovePrefix(key, prefix);
    }

    private TimeSpan? BuildTtl(TimeSpan? ttl)
    {
        if (ttl == null)
            return _option.DefaultTTL;

        if (ttl.Value.TotalSeconds < 0)
            throw new InvalidOperationException("TTL cannot be smaller than zero");

        // TTL 为 0 表示永久存储
        if (ttl.Value.TotalSeconds == 0)
            return null;

        return ttl;
    }
}
