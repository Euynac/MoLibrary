using StackExchange.Redis;

namespace Monica.StateStore.StackExchange.Scripts;

/// <summary>
/// Lua scripts for atomic Redis operations with version-based ETag.
/// Data is stored in Hash format with fields: data, version
/// ETag = version.ToString()
/// Reference: Dapr components-contrib/state/redis/redis.go
/// </summary>
public static class RedisLuaScripts
{
    /// <summary>
    /// Get value with ETag (version-based) using Hash storage
    /// @key = Redis key
    /// Returns: {data, version_string} or {nil, '0'} if not exists
    /// </summary>
    public const string GetWithETag = """
        local data = redis.call('HGET', @key, 'data')
        if data == false then
            return {false, '0'}
        end
        local version = redis.call('HGET', @key, 'version')
        return {data, tostring(version or 0)}
        """;

    /// <summary>
    /// Save state with version increment using Hash storage (with TTL)
    /// @key = Redis key
    /// @data = JSON data to store
    /// @etag = expected ETag ("0" to skip check, used for regular saves)
    /// @ttl = TTL in seconds (0 or negative = no expiry)
    /// Returns: new version as string
    /// Reference: Dapr setDefaultQuery pattern
    /// </summary>
    public const string SaveState = """
        local currentEtag = redis.pcall('HGET', @key, 'version')
        if type(currentEtag) == 'table' then
            redis.call('DEL', @key)
            currentEtag = false
        end
        if not currentEtag or currentEtag == '' or currentEtag == @etag or @etag == '0' then
            redis.call('HSET', @key, 'data', @data)
            local newVersion = redis.call('HINCRBY', @key, 'version', 1)
            local ttlNum = tonumber(@ttl)
            if ttlNum and ttlNum > 0 then
                redis.call('EXPIRE', @key, ttlNum)
            end
            return tostring(newVersion)
        else
            return error('ETag mismatch')
        end
        """;

    /// <summary>
    /// Compare-and-swap with strict ETag check using Hash storage (with TTL)
    /// @key = Redis key
    /// @etag = expected ETag (version string)
    /// @data = new JSON data
    /// @ttl = TTL in seconds (0 or negative = no expiry)
    /// Returns: new version as string if successful, nil if ETag mismatch
    /// </summary>
    public const string CompareAndSwap = """
        local currentEtag = redis.pcall('HGET', @key, 'version')
        if type(currentEtag) == 'table' then
            currentEtag = false
        end
        local currentVersion = currentEtag and tonumber(currentEtag) or 0
        if tostring(currentVersion) ~= @etag then
            return nil
        end
        redis.call('HSET', @key, 'data', @data)
        local newVersion = redis.call('HINCRBY', @key, 'version', 1)
        local ttlNum = tonumber(@ttl)
        if ttlNum and ttlNum > 0 then
            redis.call('EXPIRE', @key, ttlNum)
        end
        return tostring(newVersion)
        """;

    /// <summary>
    /// Delete with ETag verification using Hash storage
    /// @key = Redis key
    /// @etag = expected ETag (version string, "0" to skip check)
    /// Returns: 1 if deleted, 0 if ETag mismatch
    /// Reference: Dapr delDefaultQuery pattern
    /// </summary>
    public const string DeleteWithETag = """
        local currentEtag = redis.pcall('HGET', @key, 'version')
        if not currentEtag or type(currentEtag) == 'table' or currentEtag == '' then
            if @etag == '0' or @etag == '' then
                return 1
            end
            return 0
        end
        if currentEtag == @etag or @etag == '0' then
            return redis.call('DEL', @key)
        else
            return 0
        end
        """;

    /// <summary>
    /// Save state only if key does not exist (atomic first-write) using Hash storage (with TTL)
    /// @key = Redis key
    /// @data = JSON data
    /// @ttl = TTL in seconds (0 or negative = no expiry)
    /// Returns: 1 if saved (new key), 0 if key already exists
    /// </summary>
    public const string SaveIfNotExists = """
        local exists = redis.call('EXISTS', @key)
        if exists == 1 then
            return 0
        end
        redis.call('HSET', @key, 'data', @data, 'version', 1)
        local ttlNum = tonumber(@ttl)
        if ttlNum and ttlNum > 0 then
            redis.call('EXPIRE', @key, ttlNum)
        end
        return 1
        """;

    // Cached prepared scripts
    private static LuaScript? _getWithETagScript;
    private static LuaScript? _saveStateScript;
    private static LuaScript? _compareAndSwapScript;
    private static LuaScript? _deleteWithETagScript;
    private static LuaScript? _saveIfNotExistsScript;

    /// <summary>
    /// Gets the prepared GetWithETag Lua script
    /// </summary>
    public static LuaScript GetGetWithETagScript()
    {
        return _getWithETagScript ??= LuaScript.Prepare(GetWithETag);
    }

    /// <summary>
    /// Gets the prepared SaveState Lua script
    /// </summary>
    public static LuaScript GetSaveStateScript()
    {
        return _saveStateScript ??= LuaScript.Prepare(SaveState);
    }

    /// <summary>
    /// Gets the prepared CompareAndSwap Lua script
    /// </summary>
    public static LuaScript GetCompareAndSwapScript()
    {
        return _compareAndSwapScript ??= LuaScript.Prepare(CompareAndSwap);
    }

    /// <summary>
    /// Gets the prepared DeleteWithETag Lua script
    /// </summary>
    public static LuaScript GetDeleteWithETagScript()
    {
        return _deleteWithETagScript ??= LuaScript.Prepare(DeleteWithETag);
    }

    /// <summary>
    /// Gets the prepared SaveIfNotExists Lua script
    /// </summary>
    public static LuaScript GetSaveIfNotExistsScript()
    {
        return _saveIfNotExistsScript ??= LuaScript.Prepare(SaveIfNotExists);
    }
}
