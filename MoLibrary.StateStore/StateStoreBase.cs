using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.StateStore;

/// <summary>
/// 状态存储抽象基类
/// </summary>
public abstract class StateStoreBase(ILogger logger) : IMoStateStore
{
    protected readonly ILogger Logger = logger;

    #region 可以自举的方法实现

    public async Task<bool> ExistAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<T>(key, null, cancellationToken) != null;
    }

    public async Task<bool> ExistAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<T>(key, prefix, cancellationToken) != null;
    }

    public async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        return await GetBulkStateAsync<T>(keys, GetAutoPrefixFromType(typeof(T)), removePrefix, removeEmptyValue, cancellationToken);
    }


    public async Task<T?> GetStateAsync<T>(string key, 
        CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<T>(key, GetAutoPrefixFromType(typeof(T)), cancellationToken);
    }

  
    public async Task<T?> GetSingleStateAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        return await GetStateAsync<T>(GetAutoPrefixFromType(typeof(T)), null, cancellationToken);
    }

    public async Task SaveStateAsync<T>(string key, T value, 
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        await SaveStateAsync(key, value, GetAutoPrefixFromType(typeof(T)), cancellationToken, ttl);
    }

    public async Task SaveSingleStateAsync<T>(T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null) where T : class
    {
        await SaveStateAsync(GetAutoPrefixFromType(typeof(T)), value, GetAutoPrefixFromType(typeof(T)), cancellationToken, ttl);
    }

    public async Task DeleteStateAsync(string key, CancellationToken cancellationToken = default)
    {
        await DeleteStateAsync(key, null, cancellationToken);
    }

    public async Task DeleteSingleStateAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        await DeleteStateAsync(GetAutoPrefixFromType(typeof(T)), null, cancellationToken);
    }

    public async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        await DeleteBulkStateAsync(keys, null, cancellationToken);
    }

    public async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key,
        CancellationToken cancellationToken = default)
    {
        return await GetStateAndVersionAsync<T>(key, GetAutoPrefixFromType(typeof(T)), cancellationToken);
    }

    #endregion

    #region 核心抽象方法
    public abstract Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);


    public abstract Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default);

    public abstract Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    public abstract Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default);

    public abstract Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default);

    public abstract Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix,
        CancellationToken cancellationToken = default);

    #endregion

    #region 工具方法

    /// <summary>
    /// 生成带前缀的键
    /// </summary>
    /// <param name="key">原始键</param>
    /// <param name="prefix">键前缀</param>
    /// <returns>带前缀的键</returns>
    protected virtual string GetKey(string key, string? prefix = null)
    {
        return (prefix?.BeIfNotEmpty(prefix + "&&") ?? "") + key;
    }

    /// <summary>
    /// 移除键前缀
    /// </summary>
    /// <param name="key">带前缀的键</param>
    /// <param name="prefix">前缀</param>
    /// <returns>移除前缀后的键</returns>
    protected virtual string RemovePrefix(string key, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return key;
        var len = prefix.Length + 2;
        return key.Remove(0, len);
    }

    /// <summary>
    /// 根据类型自动生成前缀
    /// </summary>
    /// <param name="type">要生成前缀的类型</param>
    protected virtual string GetAutoPrefixFromType(Type type)
    {
        return type.GetCleanFullName();
    }

    #endregion
} 