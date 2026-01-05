using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;
using MoLibrary.StateStore.StackExchange.Connection;
using MoLibrary.StateStore.StackExchange.Modules;
using MoLibrary.StateStore.StackExchange.Scripts;
using StackExchange.Redis;

namespace MoLibrary.StateStore.StackExchange;

/// <summary>
/// Redis state store implementation based on StackExchange.Redis.
/// Supports Normal, Sentinel, and Cluster connection modes.
/// </summary>
public class RedisStateStore : DistributedStateStoreBase
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ModuleRedisStateStoreOption _option;
    private readonly IDatabase _database;
    private readonly IRedisConnectionFactory _connectionFactory;

    // Cached loaded scripts for performance
    private LoadedLuaScript? _getWithETagScript;
    private LoadedLuaScript? _compareAndSwapScript;
    private bool _scriptsInitialized;
    private readonly object _scriptLock = new();

    public RedisStateStore(
        IConnectionMultiplexer connection,
        IOptions<ModuleRedisStateStoreOption> options,
        IRedisConnectionFactory connectionFactory,
        ILogger<RedisStateStore> logger) : base(logger)
    {
        _connection = connection;
        _option = options.Value;
        _connectionFactory = connectionFactory;
        _database = _connection.GetDatabase(_option.DatabaseIndex);
    }

    /// <summary>
    /// Initialize Lua scripts on first use (lazy initialization)
    /// </summary>
    private void EnsureScriptsLoaded()
    {
        if (_scriptsInitialized) return;

        lock (_scriptLock)
        {
            if (_scriptsInitialized) return;

            try
            {
                var server = _connectionFactory.GetPrimaryServer(_connection);
                _getWithETagScript = RedisLuaScripts.GetGetWithETagScript().Load(server);
                _compareAndSwapScript = RedisLuaScripts.GetCompareAndSwapScript().Load(server);
                _scriptsInitialized = true;
                Logger.LogDebug("Redis Lua scripts loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load Lua scripts, will use fallback implementation");
            }
        }
    }

    /// <summary>
    /// Get the final Redis key with optional KeyPrefix configuration
    /// </summary>
    private string GetRedisKey(string key)
    {
        return string.IsNullOrEmpty(_option.KeyPrefix) ? key : $"{_option.KeyPrefix}:{key}";
    }

    /// <summary>
    /// Strip the KeyPrefix from a Redis key
    /// </summary>
    private string StripRedisKeyPrefix(string redisKey)
    {
        if (string.IsNullOrEmpty(_option.KeyPrefix))
            return redisKey;

        var prefixWithColon = $"{_option.KeyPrefix}:";
        return redisKey.StartsWith(prefixWithColon)
            ? redisKey[prefixWithColon.Length..]
            : redisKey;
    }

    public override async Task<List<string>> ScanKeysAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keys = new List<string>();
            var server = _connectionFactory.GetPrimaryServer(_connection);
            var redisPattern = GetRedisKey(pattern);

            await foreach (var key in server.KeysAsync(
                database: _option.DatabaseIndex,
                pattern: redisPattern).WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                keys.Add(StripRedisKeyPrefix(key.ToString()));
            }

            Logger.LogDebug("Found {Count} keys matching pattern: {Pattern}", keys.Count, pattern);
            return keys;
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("ScanKeysAsync was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR scanning keys with pattern {0}", pattern);
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
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKeys = keys.Select(k => (RedisKey)GetRedisKey(k)).ToArray();
        try
        {
            var values = await _database.StringGetAsync(redisKeys);

            var result = new Dictionary<string, string>();
            for (int i = 0; i < keys.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var value = values[i];
                if (!value.HasValue && removeEmptyValue)
                    continue;

                result[keys[i]] = value.ToString();
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting bulk state with keys: {0}", string.Join(", ", keys));
        }
    }

    public override async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(
        IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default) where T : default
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKeys = keys.Select(k => (RedisKey)GetRedisKey(k)).ToArray();
        try
        {
            var values = await _database.StringGetAsync(redisKeys);

            var result = new Dictionary<string, T?>();
            for (int i = 0; i < keys.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var value = values[i];
                if (!value.HasValue && removeEmptyValue)
                    continue;

                if (value.HasValue)
                {
                    try
                    {
                        var deserialized = JsonSerializer.Deserialize<T>(value.ToString());
                        result[keys[i]] = deserialized;
                    }
                    catch (Exception ex)
                    {
                        throw ex.CreateException(Logger, "Failed to deserialize value for key {0} to type {1}",
                            keys[i], typeof(T).FullName);
                    }
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting bulk state with keys: {0}", string.Join(", ", keys));
        }
    }

    public override async Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default) where T : default
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            var value = await _database.StringGetAsync(redisKey);
            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state with key: {0}", key);
        }
    }

    public override async Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            var value = await _database.StringGetAsync(redisKey);
            return value.HasValue ? value.ToString() : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state with key: {0}", key);
        }
    }

    public override async Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);

            await _database.StringSetAsync(redisKey, json, expiry);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state with key: {0}", key);
        }
    }

    public override async Task DeleteStateAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            await _database.KeyDeleteAsync(redisKey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR deleting state with key: {0}", key);
        }
    }

    public override async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKeys = keys.Select(k => (RedisKey)GetRedisKey(k)).ToArray();
        try
        {
            await _database.KeyDeleteAsync(redisKeys);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR deleting bulk state with keys: {0}", string.Join(", ", keys));
        }
    }

    public override async Task<(T? Value, string ETag)> GetStateAndVersionAsync<T>(string key, CancellationToken cancellationToken = default) where T : default
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            EnsureScriptsLoaded();

            // Use Lua script for atomic get with ETag
            if (_getWithETagScript != null)
            {
                var result = await _database.ScriptEvaluateAsync(
                    _getWithETagScript,
                    new { key = (RedisKey)redisKey });

                var results = (RedisResult[])result!;
                var valueStr = (string?)results[0];
                var etag = (string)results[1]!;

                if (valueStr == null)
                    return (default, string.Empty);

                var deserialized = JsonSerializer.Deserialize<T>(valueStr);
                return (deserialized, etag);
            }

            // Fallback: use non-atomic approach (for compatibility)
            return await GetStateAndVersionFallbackAsync<T>(redisKey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state and version with key: {0}", key);
        }
    }

    private async Task<(T? Value, string ETag)> GetStateAndVersionFallbackAsync<T>(string redisKey)
    {
        var value = await _database.StringGetAsync(redisKey);
        if (!value.HasValue)
            return (default, string.Empty);

        var valueStr = value.ToString();
        var deserialized = JsonSerializer.Deserialize<T>(valueStr);
        // Use SHA1 hash for stable ETag
        var etag = ComputeSha1Hash(valueStr);

        return (deserialized, etag);
    }

    public override async Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(
        string key,
        T value,
        string expectedETag,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            EnsureScriptsLoaded();

            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);
            var ttlSeconds = expiry?.TotalSeconds ?? -1;

            // Use Lua script for atomic compare-and-swap
            if (_compareAndSwapScript != null)
            {
                var result = await _database.ScriptEvaluateAsync(
                    _compareAndSwapScript,
                    new
                    {
                        key = (RedisKey)redisKey,
                        arg1 = expectedETag,
                        arg2 = json,
                        arg3 = (int)ttlSeconds
                    });

                if (result.IsNull)
                {
                    Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}", key, expectedETag);
                    return (false, null);
                }

                var newETag = (string)result!;
                Logger.LogDebug("Saved state with ETag: {ETag} for key: {Key}", newETag, key);
                return (true, newETag);
            }

            // Fallback: use transaction-based approach
            return await TrySaveStateWithETagFallbackAsync(redisKey, json, expectedETag, expiry);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state with ETag for key: {0}", key);
        }
    }

    private async Task<(bool Success, string? NewETag)> TrySaveStateWithETagFallbackAsync(
        string redisKey,
        string json,
        string expectedETag,
        TimeSpan? expiry)
    {
        // Fallback using WATCH + MULTI + EXEC
        var transaction = _database.CreateTransaction();

        var currentValue = await _database.StringGetAsync(redisKey);
        var currentETag = currentValue.HasValue ? ComputeSha1Hash(currentValue.ToString()) : string.Empty;

        if (currentETag != expectedETag)
        {
            Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}, Actual: {Actual}",
                redisKey, expectedETag, currentETag);
            return (false, null);
        }

        transaction.AddCondition(Condition.StringEqual(redisKey, currentValue));
        _ = transaction.StringSetAsync(redisKey, json, expiry);

        var success = await transaction.ExecuteAsync();

        if (success)
        {
            var newETag = ComputeSha1Hash(json);
            Logger.LogDebug("Saved state with ETag: {ETag} for key: {Key}", newETag, redisKey);
            return (true, newETag);
        }

        return (false, null);
    }

    public override async Task<bool> TrySaveStateIfNotExistsAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);

            // Use SET NX (only if not exists)
            var success = await _database.StringSetAsync(redisKey, json, expiry, When.NotExists);

            if (success)
            {
                Logger.LogDebug("Saved state (if not exists) with key: {Key}", key);
            }
            else
            {
                Logger.LogDebug("Key already exists, cannot save: {Key}", key);
            }

            return success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state if not exists for key: {0}", key);
        }
    }

    private TimeSpan? BuildTtl(TimeSpan? ttl)
    {
        if (ttl == null)
            return _option.DefaultTTL;

        if (ttl.Value.TotalSeconds < 0)
            throw new InvalidOperationException("TTL cannot be smaller than zero");

        // TTL of 0 means permanent storage (no expiration)
        if (ttl.Value.TotalSeconds == 0)
            return null;

        return ttl;
    }

    /// <summary>
    /// Compute SHA1 hash for stable ETag generation
    /// </summary>
    private static string ComputeSha1Hash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
