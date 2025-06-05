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
public class DaprStateStore(DaprClient dapr, ILogger<DaprStateStore> logger, IOptions<ModuleDaprStateStoreOption> options) : StateStoreBase(logger)
{
    /// <summary>
    /// 配置选项
    /// </summary>
    protected ModuleDaprStateStoreOption Option { get; set; } = options.Value;

    /// <summary>
    /// 状态存储名称
    /// </summary>
    private string StateStoreName => Option.StateStoreName;

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
            throw e.CreateException(Logger, "ERROR query state from {0} using exp: {1}", StateStoreName,
                queryStr);
        }
    }

    public override async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default) where T : default
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
                            throw e.CreateException(Logger, "Failed to deserialize JSON value \"{0}\" to type {1}", item.Value, typeof(T).GetCleanFullName());
                        }
                    });
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", finalKeys));
        }
    }

    public override async Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix,
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
            throw e.CreateException(Logger, "ERROR Getting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", finalKeys));
        }
    }

    public override async Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default) where T : default
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAsync<T>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting state from {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    public override async Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAsync<string>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting state from {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    public override async Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
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
            throw e.CreateException(Logger, "ERROR Saving state to {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    public override async Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            await dapr.DeleteStateAsync(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Deleting state from {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

    public override async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKeys = keys.Select(k => GetKey(k, prefix)).ToList();
        try
        {
            await dapr.DeleteBulkStateAsync(StateStoreName,
                finalKeys.Select(p => new BulkDeleteStateItem(p, null)).ToList(), cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Deleting bulk state from {0} with keys: {1}", StateStoreName,
                string.Join(", ", finalKeys));
        }
    }

    public override async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAndETagAsync<T>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR Getting state and version from {0} with key: {1}", StateStoreName,
                finalKey);
        }
    }

 
}