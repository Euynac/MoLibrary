using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.Dapr.Modules;
using MoLibrary.StateStore;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Dapr.StateStore;

/// <summary>
/// Dapr状态存储实现类
/// </summary>
public class DaprStateStore(DaprClient dapr, ILogger<DaprStateStore> logger, IOptions<ModuleDaprStateStoreOption> options) : IDistributedStateStore
{
    /// <summary>
    /// 配置选项
    /// </summary>
    protected ModuleDaprStateStoreOption Option { get; set; } = options.Value;

    /// <summary>
    /// 状态存储名称
    /// </summary>
    private string StateStoreName => Option.StateStoreName;

    public async Task<bool> ExistAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<T>(key, null, cancellationToken) != null;
    }

    public async Task<bool> ExistAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<T>(key, prefix, cancellationToken) != null;
    }

    public async Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class
    {
        var queryStr = "";
        try
        {
            var queryBuilder = new QueryBuilder<T>();
            var finished = query.Invoke(queryBuilder);
            queryStr = finished.ToString();
            var response =
                await dapr.QueryStateAsync<T>(StateStoreName, queryStr, cancellationToken: cancellationToken);
            return response.Results.ToDictionary(p => p.Key, item => item.Data);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR query state from {0} using exp: {1}", StateStoreName,
                queryStr);
        }
    }

    public async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        return await GetBulkStateAsync<T>(keys, GetAutoPrefixFromType(typeof(T)), removePrefix, removeEmptyValue, cancellationToken);
    }

    public async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        var finalKeys = keys.Select(k => GetKey(k, prefix)).ToList();
        try
        {
            return (await dapr.GetBulkStateAsync(StateStoreName, finalKeys, 0,
                cancellationToken: cancellationToken))
                .WhereIf(removeEmptyValue, p => !string.IsNullOrEmpty(p.Value))
                .ToDictionary(
                    p => removePrefix ? RemovePrefix(p.Key, prefix) : p.Key,
                    item =>
                    {
                        try
                        {
                            return JsonSerializer.Deserialize<T>(item.Value, dapr.JsonSerializerOptions);
                        }
                        catch (Exception e)
                        {
                            throw e.CreateException(logger, "Failed to deserialize JSON value \"{0}\" to type {1}", item.Value, typeof(T).GetCleanFullName());
                        }
                    });
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Getting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", finalKeys));
        }
    }

    public async Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        return await GetBulkStateAsync(keys, null, removePrefix, removeEmptyValue, cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        var finalKeys = keys.Select(k => GetKey(k, prefix)).ToList();
        try
        {
            return (await dapr.GetBulkStateAsync(StateStoreName, finalKeys, 0,
                cancellationToken: cancellationToken))
                .WhereIf(removeEmptyValue, p => !string.IsNullOrEmpty(p.Value))
                .ToDictionary(p => removePrefix ? RemovePrefix(p.Key, prefix) : p.Key, item => item.Value);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Getting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", finalKeys));
        }
    }

    public async Task<T?> GetStateAsync<T>(string key, 
        CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<T>(key, GetAutoPrefixFromType(typeof(T)), cancellationToken);
    }

    public async Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAsync<T>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Getting state from {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    public async Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync(key, null, cancellationToken);
    }

    public async Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAsync<string>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Getting state from {0} with key: {1}", StateStoreName,
                finalKey);
        }
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

    public async Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var ttlSeconds = ttl?.Seconds;
            switch (ttlSeconds)
            {
                case < 0:
                    throw new InvalidOperationException("ttl can not smaller than zero");
                case 0:
                    await dapr.SaveStateAsync(StateStoreName, finalKey, value, cancellationToken: cancellationToken,
                        metadata: new Dictionary<string, string> { { "ttlInSeconds", "-1" } });
                    break;
                case { } seconds:
                    await dapr.SaveStateAsync(StateStoreName, finalKey, value, cancellationToken: cancellationToken,
                        metadata: new Dictionary<string, string> { { "ttlInSeconds", seconds.ToString() } });
                    break;
                default:
                    await dapr.SaveStateAsync(StateStoreName, finalKey, value, cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Saving state to {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    public async Task SaveSingleStateAsync<T>(T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null) where T : class
    {
        await SaveStateAsync(GetAutoPrefixFromType(typeof(T)), value, GetAutoPrefixFromType(typeof(T)), cancellationToken, ttl);
    }

    public async Task DeleteStateAsync(string key, CancellationToken cancellationToken = default)
    {
        await DeleteStateAsync(key, null, cancellationToken);
    }

    public async Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            await dapr.DeleteStateAsync(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Deleting state from {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    public async Task DeleteSingleStateAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        await DeleteStateAsync(GetAutoPrefixFromType(typeof(T)), null, cancellationToken);
    }

    public async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        await DeleteBulkStateAsync(keys, null, cancellationToken);
    }

    public async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKeys = keys.Select(k => GetKey(k, prefix)).ToList();
        try
        {
            await dapr.DeleteBulkStateAsync(StateStoreName,
                finalKeys.Select(p => new BulkDeleteStateItem(p, null)).ToList(), cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Deleting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", finalKeys));
        }
    }

    public async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return await GetStateAndVersionAsync<T>(key, GetAutoPrefixFromType(typeof(T)), cancellationToken);
    }

    public async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAndETagAsync<T>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "ERROR Getting state and version from {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    /// <summary>
    /// 生成带前缀的键
    /// </summary>
    /// <param name="key">原始键</param>
    /// <param name="prefix">键前缀</param>
    /// <returns>带前缀的键</returns>
    private static string GetKey(string key, string? prefix = null)
    {
        return (prefix?.BeIfNotEmpty(prefix + "&&") ?? "") + key;
    }

    /// <summary>
    /// 移除键前缀
    /// </summary>
    /// <param name="key">带前缀的键</param>
    /// <param name="prefix">前缀</param>
    /// <returns>移除前缀后的键</returns>
    private static string RemovePrefix(string key, string? prefix)
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
}