using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoChainTracing.Implementations;
using MoLibrary.Core.Features.MoChainTracing.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链追踪扩展方法
/// </summary>
public static class ChainTracingExtensions
{
    /// <summary>
    /// 开始作用域追踪
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="operation">操作名称</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <param name="type"></param>
    /// <returns>作用域追踪器</returns>
    public static ChainTracingScope BeginScope(this IMoChainTracing chainTracing,
        string operation,
        string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        return new ChainTracingScope(chainTracing, operation, handler, extraInfo, type);
    }

    /// <summary>
    /// 执行微服务调用并自动合并远程调用链
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="serviceName">微服务名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="httpCall">HTTP 调用函数</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>操作结果</returns>
    public static async Task<T> ExecuteWithMicroserviceTraceAsync<T>(this IMoChainTracing chainTracing,
        string serviceName, string operation, Func<Task<T>> httpCall, object? extraInfo = null)
        where T : IServiceResponse
    {
        var traceId = chainTracing.BeginTrace(operation, $"Microservice({serviceName})", extraInfo);
        try
        {
            var result = await httpCall();
            
            // 自动合并远程调用链
            if (result.ExtraInfo != null)
            {
                chainTracing.MergeRemoteChain(traceId, result.ExtraInfo);
            }
            
            var success = result.Code == ResponseCode.Ok;
            chainTracing.EndTrace(traceId, $"Code: {result.Code}", success);
            
            return result;
        }
        catch (Exception ex)
        {
            chainTracing.EndTrace(traceId, $"Exception: {ex.Message}", false, ex);
            throw;
        }
    }

    /// <summary>
    /// 执行微服务调用并自动合并远程调用链（使用作用域）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="serviceName">微服务名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="httpCall">HTTP 调用函数</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>操作结果</returns>
    public static async Task<T> ExecuteWithMicroserviceScopeAsync<T>(this IMoChainTracing chainTracing,
        string serviceName, string operation, Func<ChainTracingScope, Task<T>> httpCall, object? extraInfo = null)
        where T : IServiceResponse
    {
        using var scope = chainTracing.BeginScope(operation, $"Microservice({serviceName})", extraInfo);
        try
        {
            var result = await httpCall(scope);
            
            // 自动合并远程调用链
            if (result.ExtraInfo != null)
            {
                scope.MergeRemoteChain(result.ExtraInfo);
            }
            
            var success = result.Code == ResponseCode.Ok;
            scope.EndWithSuccess($"Code: {result.Code}");
            
            return result;
        }
        catch (Exception ex)
        {
            scope.EndWithException(ex);
            throw;
        }
    }

    /// <summary>
    /// 记录微服务调用（支持调用链合并）
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="serviceName">微服务名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="response">微服务响应</param>
    /// <param name="success">是否成功</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>调用链节点标识</returns>
    public static string RecordMicroserviceCall(this IMoChainTracing chainTracing,
        string serviceName, string operation, IServiceResponse? response = null, 
        bool success = true, TimeSpan? duration = null, object? extraInfo = null)
    {
        var handler = $"Microservice({serviceName})";
        var result = response != null ? $"Code: {response.Code}" : success ? "Success" : "Failed";

        var traceId = chainTracing.BeginTrace(operation, handler, extraInfo);
        
        // 如果有响应且包含调用链信息，则合并
        if (response?.ExtraInfo != null)
        {
            chainTracing.MergeRemoteChain(traceId, response.ExtraInfo);
        }
        
        chainTracing.EndTrace(traceId, result, success);
        
        return traceId;
    }

    /// <summary>
    /// 自动处理 IServiceResponse 的调用链合并
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="response">服务响应</param>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="traceId">当前调用链节点标识</param>
    /// <param name="logger">日志记录器</param>
    /// <returns>服务响应（用于链式调用）</returns>
    public static T WithRemoteChainMerging<T>(this T response, IMoChainTracing chainTracing, 
        string traceId, ILogger? logger = null) where T : IServiceResponse
    {
        try
        {
            if (response.ExtraInfo != null)
            {
                chainTracing.MergeRemoteChain(traceId, response.ExtraInfo);
                logger?.LogDebug("自动合并远程调用链: TraceId: {TraceId}", traceId);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "自动合并远程调用链失败: TraceId: {TraceId}", traceId);
        }
        
        return response;
    }

    /// <summary>
    /// 提取调用链信息用于跨服务传递
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <returns>可传递的调用链信息</returns>
    public static object? ExtractChainForPropagation(this IMoChainTracing chainTracing)
    {
        var context = chainTracing.GetCurrentChain();
        if (context?.Root == null)
        {
            return null;
        }

        return new
        {
            chainTracing = new
            {
                rootNode = context.Root,
                totalDurationMs = context.TotalDuration,
                startTime = context.StartTime,
                endTime = context.EndTime
            }
        };
    }
}