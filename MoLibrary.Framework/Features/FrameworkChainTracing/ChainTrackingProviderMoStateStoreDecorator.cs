using System.Collections;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoChainTracing;
using MoLibrary.Core.Features.MoChainTracing.Models;
using MoLibrary.StateStore;

namespace MoLibrary.Framework.Features.FrameworkChainTracing;

/// <summary>
/// 基于调用链追踪系统的StateStore装饰器
/// 用于自动记录StateStore操作的执行情况到调用链中
/// </summary>
/// <param name="stateStore">被装饰的StateStore实例</param>
/// <param name="chainTracing">调用链追踪服务</param>
/// <param name="logger">日志记录器</param>
public class ChainTrackingProviderMoStateStoreDecorator(
    IMoStateStore stateStore,
    IMoChainTracing chainTracing,
    ILogger<ChainTrackingProviderMoStateStoreDecorator> logger) : StateStoreBase(logger)
{
    /// <summary>
    /// 记录StateStore操作开始
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>调用链节点标识</returns>
    private string BeginStateStoreTrace(string operationName, string? key = null, string? prefix = null, object? extraInfo = null)
    {
        try
        {
            var operationDescription = operationName;
            if (!string.IsNullOrEmpty(key))
            {
                var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
                operationDescription += $"({fullKey})";
            }

            return chainTracing.BeginTrace(operationDescription, null, extraInfo, EChainTracingType.StateStore);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "记录StateStore操作开始时发生异常");
            return string.Empty;
        }
    }

    /// <summary>
    /// 记录StateStore操作结束
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="success">是否成功</param>
    /// <param name="exception">异常信息</param>
    /// <param name="result">操作结果</param>
    private void EndStateStoreTrace(string traceId, bool success = true, Exception? exception = null, object? result = null)
    {
        if (string.IsNullOrEmpty(traceId))
            return;

        try
        {
            string resultDescription = success ? "Success" : "Failed";
            
            if (result != null)
            {
                if (result is bool boolResult)
                {
                    resultDescription += $"[Result:{boolResult}]";
                }
                else if (result is IDictionary dictResult)
                {
                    resultDescription += $"[Count:{dictResult.Count}]";
                }
                else if (result is Array arrayResult)
                {
                    resultDescription += $"[Count:{arrayResult.Length}]";
                }
                else if (result.GetType().IsGenericType && 
                         result.GetType().GetGenericTypeDefinition() == typeof(ValueTuple<,>))
                {
                    resultDescription += "[WithETag]";
                }
            }

            chainTracing.EndTrace(traceId, resultDescription, success, exception);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "记录StateStore操作结束时发生异常");
        }
    }

    /// <summary>
    /// 执行带有调用链追踪的StateStore操作
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="operationName">操作名称</param>
    /// <param name="operation">要执行的操作</param>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>操作结果</returns>
    private async Task<T> ExecuteWithTracing<T>(
        string operationName, 
        Func<Task<T>> operation, 
        string? key = null, 
        string? prefix = null, 
        object? extraInfo = null)
    {
        var traceId = BeginStateStoreTrace(operationName, key, prefix, extraInfo);
        
        try
        {
            var result = await operation();
            EndStateStoreTrace(traceId, true, null, result);
            return result;
        }
        catch (Exception ex)
        {
            EndStateStoreTrace(traceId, false, ex);
            throw;
        }
    }

    /// <summary>
    /// 执行带有调用链追踪的StateStore操作（无返回值）
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="operation">要执行的操作</param>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>操作任务</returns>
    private async Task ExecuteWithTracing(
        string operationName, 
        Func<Task> operation, 
        string? key = null, 
        string? prefix = null, 
        object? extraInfo = null)
    {
        var traceId = BeginStateStoreTrace(operationName, key, prefix, extraInfo);
        
        try
        {
            await operation();
            EndStateStoreTrace(traceId, true);
        }
        catch (Exception ex)
        {
            EndStateStoreTrace(traceId, false, ex);
            throw;
        }
    }

    #region IMoStateStore 实现

    public override async Task<bool> ExistAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var extraInfo = new { Type = typeof(T).Name };
        return await ExecuteWithTracing(
            "ExistAsync", 
            () => stateStore.ExistAsync<T>(key, cancellationToken), 
            key, 
            typeof(T).Name, 
            extraInfo);
    }

    public override async Task<bool> ExistAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var extraInfo = new { Type = typeof(T).Name, Prefix = prefix };
        return await ExecuteWithTracing(
            "ExistAsync", 
            () => stateStore.ExistAsync<T>(key, prefix, cancellationToken), 
            key, 
            prefix, 
            extraInfo);
    }

   
    public override async Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix, bool removePrefix = true, bool removeEmptyValue = true, CancellationToken cancellationToken = default) where T : default
    {
        var extraInfo = new 
        { 
            Type = typeof(T).Name, 
            KeyCount = keys.Count, 
            Prefix = prefix, 
            RemovePrefix = removePrefix, 
            RemoveEmptyValue = removeEmptyValue,
            Keys = keys.Take(5).ToArray()
        };
        return await ExecuteWithTracing(
            "GetBulkStateAsync", 
            () => stateStore.GetBulkStateAsync<T>(keys, prefix, removePrefix, removeEmptyValue, cancellationToken), 
            $"[{keys.Count} keys]", 
            prefix, 
            extraInfo);
    }


    public override async Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default) where T : default
    {
        var extraInfo = new { Type = typeof(T).Name, Prefix = prefix };
        return await ExecuteWithTracing(
            "GetStateAsync", 
            () => stateStore.GetStateAsync<T>(key, prefix, cancellationToken), 
            key, 
            prefix, 
            extraInfo);
    }

   

    public override async Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
    {
        var extraInfo = new 
        { 
            Type = typeof(T).Name, 
            Prefix = prefix, 
            TTL = ttl?.ToString(),
            ValueSize = value?.ToString()?.Length ?? 0
        };
        await ExecuteWithTracing(
            "SaveStateAsync", 
            () => stateStore.SaveStateAsync(key, value, prefix, cancellationToken, ttl), 
            key, 
            prefix, 
            extraInfo);
    }

  

    public override async Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var extraInfo = new { Prefix = prefix };
        await ExecuteWithTracing(
            "DeleteStateAsync", 
            () => stateStore.DeleteStateAsync(key, prefix, cancellationToken), 
            key, 
            prefix, 
            extraInfo);
    }

  
    public override async Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default)
    {
        var extraInfo = new 
        { 
            KeyCount = keys.Count, 
            Prefix = prefix,
            Keys = keys.Take(5).ToArray()
        };
        await ExecuteWithTracing(
            "DeleteBulkStateAsync", 
            () => stateStore.DeleteBulkStateAsync(keys, prefix, cancellationToken), 
            $"[{keys.Count} keys]", 
            prefix, 
            extraInfo);
    }

    public override async Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        var extraInfo = new { Type = typeof(T).Name, Prefix = prefix };
        return await ExecuteWithTracing(
            "GetStateAndVersionAsync", 
            () => stateStore.GetStateAndVersionAsync<T>(key, prefix, cancellationToken), 
            key, 
            prefix, 
            extraInfo);
    }

    #endregion
}