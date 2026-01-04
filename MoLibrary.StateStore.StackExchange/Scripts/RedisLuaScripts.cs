using StackExchange.Redis;

namespace MoLibrary.StateStore.StackExchange.Scripts;

/// <summary>
/// Lua scripts for atomic Redis operations
/// </summary>
public static class RedisLuaScripts
{
    /// <summary>
    /// Get value with ETag (SHA1 based for atomicity within Redis)
    /// KEYS[1] = key
    /// Returns: {value, etag} or {nil, ''} if not exists
    /// </summary>
    public const string GetWithETag = """
        local value = redis.call('GET', KEYS[1])
        if value == false then
            return {false, ''}
        end
        return {value, redis.sha1hex(value)}
        """;

    /// <summary>
    /// Compare-and-swap with ETag (SHA1 based)
    /// KEYS[1] = key
    /// ARGV[1] = expected ETag (SHA1 of current value)
    /// ARGV[2] = new value
    /// ARGV[3] = TTL in seconds (-1 for no TTL)
    /// Returns: new ETag if successful, nil if ETag mismatch
    /// </summary>
    public const string CompareAndSwap = """
        local current = redis.call('GET', KEYS[1])
        local expectedEtag = ARGV[1]
        local newValue = ARGV[2]
        local ttl = tonumber(ARGV[3])

        -- Calculate current ETag using SHA1
        local currentEtag = ''
        if current then
            currentEtag = redis.sha1hex(current)
        end

        -- ETag mismatch check
        if currentEtag ~= expectedEtag then
            return nil
        end

        -- Set new value with optional TTL
        if ttl and ttl > 0 then
            redis.call('SET', KEYS[1], newValue, 'EX', ttl)
        else
            redis.call('SET', KEYS[1], newValue)
        end

        return redis.sha1hex(newValue)
        """;

    // Cached prepared scripts
    private static LuaScript? _getWithETagScript;
    private static LuaScript? _compareAndSwapScript;

    /// <summary>
    /// Gets the prepared GetWithETag Lua script
    /// </summary>
    public static LuaScript GetGetWithETagScript()
    {
        return _getWithETagScript ??= LuaScript.Prepare(GetWithETag);
    }

    /// <summary>
    /// Gets the prepared CompareAndSwap Lua script
    /// </summary>
    public static LuaScript GetCompareAndSwapScript()
    {
        return _compareAndSwapScript ??= LuaScript.Prepare(CompareAndSwap);
    }
}
