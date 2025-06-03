using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Dapr.Modules;
using MoLibrary.StateStore;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Dapr.StateStore;

public class DaprStateStore(DaprClient dapr, ILogger<DaprStateStore> logger, IOptions<ModuleDaprStateStoreOption> options) : IStateStore
{
    protected ModuleDaprStateStoreOption Option { get; set; } = options.Value;
    private string StateStoreName => Option.StateStoreName;

    public async Task<bool> ExistAsync<T>(string key, string? prefix = null, CancellationToken cancellationToken = default)
    {
        return await GetStateAsync<T>(key, prefix, cancellationToken) != null;
    }

    public async Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T:class
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
            logger.LogError(e, "ERROR query state from {StateStoreName} using exp: {exp}", StateStoreName,
                queryStr);
            throw;
        }

    }


    public async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix = null,
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
                            throw new Exception($"无法将Json值：\"{item.Value}\"反序列化为：{typeof(T).FullName}");
                        }
                    });
        }
        catch (Exception e)
        {
            logger.LogError(e, "ERROR Getting bulk state from {StateStoreName}{Keys}", StateStoreName,
                               finalKeys);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix = null,
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
            logger.LogError(e, "ERROR Getting bulk state from {StateStoreName}{Keys}", StateStoreName,
                finalKeys);
            throw;
        }
    }

    public async Task<string?> GetStateAsync(string key, string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAsync<string>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ERROR Getting state from {StateStoreName}{Key}", StateStoreName,
                finalKey);
            throw;
        }
    }

    public async Task<T?> GetStateAsync<T>(string key, string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAsync<T>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ERROR Getting state from {StateStoreName}{Key}", StateStoreName,
                finalKey);
            throw;
        }
    }

  

    public async Task SaveStateAsync(string key, object value, string? prefix = null,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
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
                case {} seconds:
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
            logger.LogError(e, "ERROR Saving state to {StateStoreName}{Key}", StateStoreName,
                finalKey);
            throw;
        }
    }


    public async Task DeleteStateAsync(string key, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            await dapr.DeleteStateAsync(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ERROR Deleting state from {StateStoreName}{Key}", StateStoreName,
                finalKey);
            throw;
        }
    }


    public async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var finalKeys = keys.Select(k => GetKey(k, prefix)).ToList();
        try
        {
            await dapr.DeleteBulkStateAsync(StateStoreName,
                finalKeys.Select(p => new BulkDeleteStateItem(p, null)).ToList(), cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ERROR Getting bulk state from {StateStoreName}{Keys}", StateStoreName,
                finalKeys);
            throw;
        }
    }

    public async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            return await dapr.GetStateAndETagAsync<T>(StateStoreName, finalKey, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ERROR Getting state from {StateStoreName}{Key}", StateStoreName,
                finalKey);
            throw;
        }
    }


    private static string GetKey(string key, string? prefix = null)
    {
        return (prefix?.BeIfNotEmpty(prefix + "&&") ?? "") + key;
    }

    private static string RemovePrefix(string key, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return key;
        var len = prefix.Length + 2;
        return key.Remove(0, len);
    }

   
}