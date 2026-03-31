using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.StateStore.StackExchange.Connection;
using Monica.Modules;
using Monica.StateStore.StackExchange.Scripts;
using Monica.StateStore.StateStore.Abstractions;
using Monica.StateStore.StateStore.Models;
using Monica.StateStore.StateStore.Queries;
using StackExchange.Redis;

namespace Monica.StateStore.StackExchange;

/// <summary>
/// Redis state store implementation based on StackExchange.Redis.
/// Uses Hash storage format with fields: data, version
/// ETag = version.ToString() (increments on each save)
/// TTL is set atomically within Lua scripts via EXPIRE command
/// Reference: Dapr components-contrib/state/redis/redis.go
/// </summary>
public class RedisStateStore : DistributedStateStoreBase, IStateStoreKeyTtlReader
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ModuleRedisStateStoreOption _option;
    private readonly IDatabase _database;
    private readonly IRedisConnectionFactory _connectionFactory;

    // Cached loaded scripts for performance
    private LoadedLuaScript? _getWithETagScript;
    private LoadedLuaScript? _saveStateScript;
    private LoadedLuaScript? _compareAndSwapScript;
    private LoadedLuaScript? _deleteWithETagScript;
    private LoadedLuaScript? _saveIfNotExistsScript;
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
    /// Initialize Lua scripts on first use (lazy initialization).
    /// Throws exception if scripts cannot be loaded.
    /// </summary>
    private void EnsureScriptsLoaded()
    {
        if (_scriptsInitialized) return;

        lock (_scriptLock)
        {
            if (_scriptsInitialized) return;

            var server = _connectionFactory.GetPrimaryServer(_connection);
            _getWithETagScript = RedisLuaScripts.GetGetWithETagScript().Load(server);
            _saveStateScript = RedisLuaScripts.GetSaveStateScript().Load(server);
            _compareAndSwapScript = RedisLuaScripts.GetCompareAndSwapScript().Load(server);
            _deleteWithETagScript = RedisLuaScripts.GetDeleteWithETagScript().Load(server);
            _saveIfNotExistsScript = RedisLuaScripts.GetSaveIfNotExistsScript().Load(server);
            _scriptsInitialized = true;
            Logger.LogDebug("Redis Lua scripts loaded successfully");
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

    public override async Task<Dictionary<string, string>> GetRawBulkStateAsync(
        IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (keys.Count == 0) return new Dictionary<string, string>();

        try
        {
            // Use batch/pipeline for efficient Hash field retrieval
            var batch = _database.CreateBatch();
            var tasks = keys.Select(k => batch.HashGetAsync(GetRedisKey(k), "data")).ToList();
            batch.Execute();
            var values = await Task.WhenAll(tasks);

            var result = new Dictionary<string, string>();
            for (int i = 0; i < keys.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var value = values[i];
                if (!value.HasValue && removeEmptyValue)
                    continue;

                if (value.HasValue)
                {
                    result[keys[i]] = value.ToString();
                }
                else if (!removeEmptyValue)
                {
                    result[keys[i]] = string.Empty;
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

    public override async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(
        IReadOnlyList<string> keys,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default) where T : default
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (keys.Count == 0) return new Dictionary<string, T?>();

        try
        {
            // Use batch/pipeline for efficient Hash field retrieval
            var batch = _database.CreateBatch();
            var tasks = keys.Select(k => batch.HashGetAsync(GetRedisKey(k), "data")).ToList();
            batch.Execute();
            var values = await Task.WhenAll(tasks);

            var result = new Dictionary<string, T?>();
            for (int i = 0; i < keys.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var value = values[i];
                if (!value.HasValue && removeEmptyValue)
                    continue;

                if (value.HasValue)
                {
                    result[keys[i]] = JsonSerializer.Deserialize<T>(value.ToString());
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
            var data = await _database.HashGetAsync(redisKey, "data");
            if (!data.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(data.ToString());
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

    public override async Task<string?> GetRawStateAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            var data = await _database.HashGetAsync(redisKey, "data");
            if (!data.HasValue)
                return null;

            return data.ToString();
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

    public async Task<StateStoreKeyTtlSnapshot> GetKeyTtlAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            var ttl = await _database.KeyTimeToLiveAsync(redisKey);
            if (ttl.HasValue)
            {
                return StateStoreKeyTtlSnapshot.FromRemaining(ttl.Value);
            }

            if (!await _database.KeyExistsAsync(redisKey))
            {
                throw new KeyNotFoundException($"Key not found: {key}");
            }

            return StateStoreKeyTtlSnapshot.Permanent;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting TTL for key: {0}", key);
        }
    }

    public override async Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            EnsureScriptsLoaded();

            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);
            var ttlSeconds = (int)(expiry?.TotalSeconds ?? 0);

            // Execute Lua script with TTL included
            await _database.ScriptEvaluateAsync(
                _saveStateScript!,
                new
                {
                    key = (RedisKey)redisKey,
                    data = json,
                    etag = "0", // Skip ETag check for regular save
                    ttl = ttlSeconds
                });
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

    public override async Task<(T? Value, string ETag)> GetStateAndETagAsync<T>(string key, CancellationToken cancellationToken = default) where T : default
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            EnsureScriptsLoaded();

            var result = await _database.ScriptEvaluateAsync(
                _getWithETagScript!,
                new { key = (RedisKey)redisKey });

            var results = (RedisResult[])result!;
            var dataJson = (string?)results[0];
            var etag = (string)results[1]!;

            if (dataJson == null || etag == "0")
                return (default, string.Empty);

            var deserialized = JsonSerializer.Deserialize<T>(dataJson);
            return (deserialized, etag);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state and ETag with key: {0}", key);
        }
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
            var ttlSeconds = (int)(expiry?.TotalSeconds ?? 0);

            // Execute CAS script with TTL included
            var result = await _database.ScriptEvaluateAsync(
                _compareAndSwapScript!,
                new
                {
                    key = (RedisKey)redisKey,
                    etag = expectedETag,
                    data = json,
                    ttl = ttlSeconds
                });

            if (result.IsNull)
            {
                Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}", key, expectedETag);
                return (false, null);
            }

            var newETag = (string)result!;
            return (true, newETag);
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
            EnsureScriptsLoaded();

            var dataJson = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);
            var ttlSeconds = (int)(expiry?.TotalSeconds ?? 0);

            // Use atomic Lua script for first-write with TTL included
            var result = await _database.ScriptEvaluateAsync(
                _saveIfNotExistsScript!,
                new
                {
                    key = (RedisKey)redisKey,
                    data = dataJson,
                    ttl = ttlSeconds
                });

            var success = (int)result == 1;

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

    public override async Task SaveBulkStateAsync<T>(
        IReadOnlyList<(string Key, T Value)> items,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        if (items.Count == 0) return;

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureScriptsLoaded();

            var expiry = BuildTtl(ttl);
            var ttlSeconds = (int)(expiry?.TotalSeconds ?? 0);

            // Use batch/pipeline for better performance
            var batch = _database.CreateBatch();
            var saveTasks = new List<Task>(items.Count);

            // Execute Lua scripts with TTL included
            foreach (var (key, value) in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var redisKey = GetRedisKey(key);
                var json = JsonSerializer.Serialize(value);

                var task = batch.ScriptEvaluateAsync(
                    _saveStateScript!,
                    new
                    {
                        key = (RedisKey)redisKey,
                        data = json,
                        etag = "0", // Skip ETag check for regular save
                        ttl = ttlSeconds
                    });
                saveTasks.Add(task);
            }

            batch.Execute();
            await Task.WhenAll(saveTasks);

            Logger.LogDebug("Saved {Count} states in bulk", items.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving bulk state with {0} items", items.Count);
        }
    }

    public override async Task<bool> TryDeleteStateWithETagAsync(
        string key,
        string expectedETag,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);
        try
        {
            EnsureScriptsLoaded();

            var result = await _database.ScriptEvaluateAsync(
                _deleteWithETagScript!,
                new
                {
                    key = (RedisKey)redisKey,
                    etag = expectedETag
                });

            var success = (int)result == 1;

            if (success)
            {
                Logger.LogDebug("Deleted state with ETag for key: {Key}", key);
            }
            else
            {
                Logger.LogDebug("ETag mismatch for delete, key: {Key}. Expected: {Expected}", key, expectedETag);
            }

            return success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR deleting state with ETag for key: {0}", key);
        }
    }
}
