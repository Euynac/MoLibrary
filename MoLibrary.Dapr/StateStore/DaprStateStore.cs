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
public class DaprStateStore(DaprClient dapr, ILogger<DaprStateStore> logger, IOptions<ModuleDaprStateStoreOption> options) : DistributedStateStoreBase(logger)
{
    /// <summary>
    /// 配置选项
    /// </summary>
    protected ModuleDaprStateStoreOption Option { get; set; } = options.Value;

    /// <summary>
    /// 状态存储名称
    /// </summary>
    private string StateStoreName => Option.StateStoreName;

    public override async Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class
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
            return (await dapr.GetBulkStateAsync(StateStoreName, finalKeys, Option.DefaultBulkParallelism,
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
            return (await dapr.GetBulkStateAsync(StateStoreName, finalKeys, Option.DefaultBulkParallelism,
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
            var metadata = BuildTtlMetadata(ttl);
            await dapr.SaveStateAsync(StateStoreName, finalKey, (object?)value, metadata: metadata, cancellationToken: cancellationToken);
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

    public override async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix,
        CancellationToken cancellationToken = default)
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

    public override async Task<(bool Success, string? NewETag)> TrySaveStateWithETagAsync<T>(string key, T value, string expectedETag,
        string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            var metadata = BuildTtlMetadata(ttl);

            // 使用 Dapr 的 TrySaveStateAsync 进行乐观锁保存
            var success = await dapr.TrySaveStateAsync(StateStoreName, finalKey, value, expectedETag,
                metadata: metadata, cancellationToken: cancellationToken);

            if (success)
            {
                // 保存成功，获取新的 ETag
                var (_, newETag) = await dapr.GetStateAndETagAsync<T>(StateStoreName, finalKey,
                    cancellationToken: cancellationToken);
                return (true, newETag);
            }

            Logger.LogDebug("ETag mismatch for key: {Key}. Expected: {Expected}", finalKey, expectedETag);
            return (false, null);
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR TrySaveStateWithETag to {0} with key: {1}", StateStoreName, finalKey);
        }
    }

    public override async Task<bool> TrySaveStateIfNotExistsAsync<T>(string key, T value, string? prefix,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var finalKey = GetKey(key, prefix);
        try
        {
            // 先检查 key 是否存在
            var (existingValue, existingETag) = await dapr.GetStateAndETagAsync<T>(StateStoreName, finalKey,
                cancellationToken: cancellationToken);

            // 如果 ETag 不为空，说明 key 已存在
            if (!string.IsNullOrEmpty(existingETag))
            {
                Logger.LogDebug("Key already exists, cannot save: {Key}", finalKey);
                return false;
            }

            // Key 不存在，尝试保存（使用空 ETag 确保是新建操作）
            var metadata = BuildTtlMetadata(ttl);

            // 使用空 ETag 进行保存，如果同时有其他进程创建了这个 key，会失败
            var success = await dapr.TrySaveStateAsync(StateStoreName, finalKey, value, "",
                metadata: metadata, cancellationToken: cancellationToken);

            if (success)
            {
                Logger.LogDebug("Saved state (if not exists) with key: {Key}", finalKey);
                return true;
            }

            // 可能在检查和保存之间有其他进程创建了这个 key
            Logger.LogDebug("Failed to save state (race condition), key: {Key}", finalKey);
            return false;
        }
        catch (Exception e)
        {
            throw e.CreateException(Logger, "ERROR TrySaveStateIfNotExists to {0} with key: {1}", StateStoreName, finalKey);
        }
    }

    private static Dictionary<string, string>? BuildTtlMetadata(TimeSpan? ttl)
    {
        var ttlSeconds = ttl?.TotalSeconds;
        return ttlSeconds switch
        {
            < 0 => throw new InvalidOperationException("ttl can not be smaller than zero"),
            0 => new Dictionary<string, string> { { "ttlInSeconds", "-1" } },
            { } seconds => new Dictionary<string, string> { { "ttlInSeconds", ((int)seconds).ToString() } },
            _ => null
        };
    }
}