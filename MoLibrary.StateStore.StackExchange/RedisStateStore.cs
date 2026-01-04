using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;
using MoLibrary.StateStore.StackExchange.Connection;
using MoLibrary.StateStore.StackExchange.Modules;
using MoLibrary.StateStore.StackExchange.Scripts;
using MoLibrary.Tool.Extensions;
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

    public override async Task<List<string>> ScanKeysAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keys = new List<string>();
            var server = _connectionFactory.GetPrimaryServer(_connection);

            await foreach (var key in server.KeysAsync(
                database: _option.DatabaseIndex,
                pattern: pattern).WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                keys.Add(key.ToString());
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
        string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKeys = keys.Select(k => (RedisKey)GetKey(k, prefix)).ToArray();
        try
        {
            var values = await _database.StringGetAsync(finalKeys);

            var result = new Dictionary<string, string>();
            for (int i = 0; i < finalKeys.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var value = values[i];
                if (!value.HasValue && removeEmptyValue)
                    continue;

                var keyStr = finalKeys[i].ToString();
                var cleanKey = removePrefix ? RemovePrefix(keyStr, prefix) : keyStr;
                result[cleanKey] = value.ToString();
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
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
        cancellationToken.ThrowIfCancellationRequested();

        var finalKeys = keys.Select(k => (RedisKey)GetKey(k, prefix)).ToArray();
        try
        {
            var values = await _database.StringGetAsync(finalKeys);

            var result = new Dictionary<string, T?>();
            for (int i = 0; i < finalKeys.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting bulk state with keys: {0}",
                string.Join(", ", finalKeys.Select(k => k.ToString())));
        }
    }

    public override async Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default) where T : default
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKey = GetKey(key, prefix);
        try
        {
            var value = await _database.StringGetAsync(finalKey);
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
            throw ex.CreateException(Logger, "ERROR getting state with key: {0}", finalKey);
        }
    }

    public override async Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKey = GetKey(key, prefix);
        try
        {
            var value = await _database.StringGetAsync(finalKey);
            return value.HasValue ? value.ToString() : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state with key: {0}", finalKey);
        }
    }

    public override async Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKey = GetKey(key, prefix);
        try
        {
            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);

            await _database.StringSetAsync(finalKey, json, expiry);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state with key: {0}", finalKey);
        }
    }

    public override async Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKey = GetKey(key, prefix);
        try
        {
            await _database.KeyDeleteAsync(finalKey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR deleting state with key: {0}", finalKey);
        }
    }

    public override async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKeys = keys.Select(k => (RedisKey)GetKey(k, prefix)).ToArray();
        try
        {
            await _database.KeyDeleteAsync(finalKeys);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR deleting bulk state with keys: {0}",
                string.Join(", ", finalKeys.Select(k => k.ToString())));
        }
    }

    public override async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKey = GetKey(key, prefix);
        try
        {
            EnsureScriptsLoaded();

            // Use Lua script for atomic get with ETag
            if (_getWithETagScript != null)
            {
                var result = await _database.ScriptEvaluateAsync(
                    _getWithETagScript,
                    new { key = (RedisKey)finalKey });

                var results = (RedisResult[])result!;
                var valueStr = (string?)results[0];
                var etag = (string)results[1]!;

                if (valueStr == null)
                    return (default(T)!, string.Empty);

                var deserialized = JsonSerializer.Deserialize<T>(valueStr);
                return (deserialized!, etag);
            }

            // Fallback: use non-atomic approach (for compatibility)
            return await GetStateAndVersionFallbackAsync<T>(finalKey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR getting state and version with key: {0}", finalKey);
        }
    }

    private async Task<(T value, string etag)> GetStateAndVersionFallbackAsync<T>(string finalKey)
    {
        var value = await _database.StringGetAsync(finalKey);
        if (!value.HasValue)
            return (default(T)!, string.Empty);

        var valueStr = value.ToString();
        var deserialized = JsonSerializer.Deserialize<T>(valueStr);
        // Use SHA1 hash for stable ETag
        var etag = ComputeSha1Hash(valueStr);

        return (deserialized!, etag);
    }

    public override async Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(
        string key,
        T value,
        string expectedETag,
        string? prefix,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKey = GetKey(key, prefix);
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
                        key = (RedisKey)finalKey,
                        arg1 = expectedETag,
                        arg2 = json,
                        arg3 = (int)ttlSeconds
                    });

                if (result.IsNull)
                {
                    Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}", finalKey, expectedETag);
                    return (false, null);
                }

                var newETag = (string)result!;
                Logger.LogDebug("Saved state with ETag: {ETag} for key: {Key}", newETag, finalKey);
                return (true, newETag);
            }

            // Fallback: use transaction-based approach
            return await TrySaveStateWithETagFallbackAsync(finalKey, json, expectedETag, expiry);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state with ETag for key: {0}", finalKey);
        }
    }

    private async Task<(bool Success, string? NewETag)> TrySaveStateWithETagFallbackAsync(
        string finalKey,
        string json,
        string expectedETag,
        TimeSpan? expiry)
    {
        // Fallback using WATCH + MULTI + EXEC
        var transaction = _database.CreateTransaction();

        var currentValue = await _database.StringGetAsync(finalKey);
        var currentETag = currentValue.HasValue ? ComputeSha1Hash(currentValue.ToString()) : string.Empty;

        if (currentETag != expectedETag)
        {
            Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}, Actual: {Actual}",
                finalKey, expectedETag, currentETag);
            return (false, null);
        }

        transaction.AddCondition(Condition.StringEqual(finalKey, currentValue));
        _ = transaction.StringSetAsync(finalKey, json, expiry);

        var success = await transaction.ExecuteAsync();

        if (success)
        {
            var newETag = ComputeSha1Hash(json);
            Logger.LogDebug("Saved state with ETag: {ETag} for key: {Key}", newETag, finalKey);
            return (true, newETag);
        }

        return (false, null);
    }

    public override async Task<bool> TrySaveStateIfNotExistsAsync<T>(
        string key,
        T value,
        string? prefix,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalKey = GetKey(key, prefix);
        try
        {
            var json = JsonSerializer.Serialize(value);
            var expiry = BuildTtl(ttl);

            // Use SET NX (only if not exists)
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(Logger, "ERROR saving state if not exists for key: {0}", finalKey);
        }
    }

    /// <summary>
    /// Generate key with prefix. If KeyPrefix is configured, prepend it.
    /// </summary>
    protected override string GetKey(string key, string? prefix = null)
    {
        var baseKey = base.GetKey(key, prefix);
        return string.IsNullOrEmpty(_option.KeyPrefix) ? baseKey : $"{_option.KeyPrefix}:{baseKey}";
    }

    /// <summary>
    /// Remove key prefix, including KeyPrefix and business prefix.
    /// </summary>
    protected override string RemovePrefix(string key, string? prefix)
    {
        // Remove KeyPrefix first
        if (!string.IsNullOrEmpty(_option.KeyPrefix))
        {
            var keyPrefixWithColon = $"{_option.KeyPrefix}:";
            if (key.StartsWith(keyPrefixWithColon))
            {
                key = key[keyPrefixWithColon.Length..];
            }
        }

        // Then remove business prefix
        return base.RemovePrefix(key, prefix);
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
