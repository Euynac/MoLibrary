using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 基于 AsyncLocal 的调用链追踪实现
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="logger">日志记录器</param>
public class AsyncLocalMoChainTracing(ILogger<AsyncLocalMoChainTracing>? logger = null) : IMoChainTracing
{
    private static readonly AsyncLocal<MoChainContext?> _chainContext = new();

    /// <summary>
    /// 开始一个新的调用链节点
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>调用链节点标识</returns>
    public string BeginTrace(string handler, string operation, object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value ??= new MoChainContext();

            var node = new MoChainNode
            {
                Handler = handler,
                Operation = operation,
                StartExtraInfo = extraInfo,
                StartTime = DateTime.UtcNow
            };

            context.AddNode(node);

            logger?.LogDebug("开始调用链节点: {Handler}.{Operation}, TraceId: {TraceId}", 
                handler, operation, node.TraceId);

            return node.TraceId;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "开始调用链节点时发生异常: {Handler}.{Operation}", handler, operation);
            return Guid.NewGuid().ToString("N"); // 返回一个虚拟的 TraceId，避免后续调用报错
        }
    }

    /// <summary>
    /// 完成一个调用链节点
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="result">调用结果描述</param>
    /// <param name="success">是否成功</param>
    /// <param name="extraInfo">额外信息</param>
    public void EndTrace(string traceId, string? result = null, bool success = true, object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value;
            if (context == null)
            {
                logger?.LogWarning("尝试完成调用链节点但当前上下文为空: TraceId: {TraceId}", traceId);
                return;
            }

            context.CompleteNode(traceId, result, success, extraInfo);

            logger?.LogDebug("完成调用链节点: TraceId: {TraceId}, Success: {Success}, Result: {Result}", 
                traceId, success, result);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "完成调用链节点时发生异常: TraceId: {TraceId}", traceId);
        }
    }

    /// <summary>
    /// 记录异常信息
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="exception">异常信息</param>
    /// <param name="extraInfo">额外信息</param>
    public void RecordException(string traceId, Exception exception, object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value;
            if (context == null)
            {
                logger?.LogWarning("尝试记录异常但当前上下文为空: TraceId: {TraceId}", traceId);
                return;
            }

            context.RecordException(traceId, exception, extraInfo);

            logger?.LogDebug("记录调用链异常: TraceId: {TraceId}, Exception: {ExceptionMessage}", 
                traceId, exception.Message);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "记录调用链异常时发生异常: TraceId: {TraceId}", traceId);
        }
    }

    /// <summary>
    /// 记录简单的调用信息
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="result">调用结果</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    public void RecordTrace(string handler, string operation, bool success = true, string? result = null, 
        TimeSpan? duration = null, object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value ??= new MoChainContext();

            var node = new MoChainNode
            {
                Handler = handler,
                Operation = operation,
                StartTime = DateTime.UtcNow,
                Success = success,
                Result = result,
                StartExtraInfo = extraInfo,
                EndExtraInfo = extraInfo
            };

            if (duration.HasValue)
            {
                node.EndTime = node.StartTime.Add(duration.Value);
            }
            else
            {
                node.EndTime = DateTime.UtcNow;
            }

            context.AddNode(node);
            context.CompleteNode(node.TraceId, result, success, extraInfo);

            logger?.LogDebug("记录简单调用链: {Handler}.{Operation}, Success: {Success}, Duration: {Duration}ms", 
                handler, operation, success, node.DurationMs);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "记录简单调用链时发生异常: {Handler}.{Operation}", handler, operation);
        }
    }

    /// <summary>
    /// 获取当前的调用链信息
    /// </summary>
    /// <returns>调用链信息</returns>
    public MoChainContext? GetCurrentChain()
    {
        return _chainContext.Value;
    }

    /// <summary>
    /// 清空当前的调用链信息
    /// </summary>
    public void ClearChain()
    {
        try
        {
            var context = _chainContext.Value;
            if (context != null)
            {
                context.MarkComplete();
                logger?.LogDebug("清空调用链上下文, 总耗时: {TotalDuration}ms", context.TotalDurationMs);
            }

            _chainContext.Value = null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "清空调用链上下文时发生异常");
        }
    }

    /// <summary>
    /// 使用指定的调用链上下文执行操作
    /// </summary>
    /// <param name="context">调用链上下文</param>
    /// <param name="action">要执行的操作</param>
    public async Task ExecuteWithChainAsync(MoChainContext? context, Func<Task> action)
    {
        var originalContext = _chainContext.Value;
        try
        {
            _chainContext.Value = context;
            await action();
        }
        finally
        {
            _chainContext.Value = originalContext;
        }
    }

    /// <summary>
    /// 使用指定的调用链上下文执行操作
    /// </summary>
    /// <param name="context">调用链上下文</param>
    /// <param name="func">要执行的操作</param>
    public async Task<T> ExecuteWithChainAsync<T>(MoChainContext? context, Func<Task<T>> func)
    {
        var originalContext = _chainContext.Value;
        try
        {
            _chainContext.Value = context;
            return await func();
        }
        finally
        {
            _chainContext.Value = originalContext;
        }
    }

    /// <summary>
    /// 开始一个根调用链
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>调用链节点标识</returns>
    public static string BeginRootTrace(string handler, string operation, object? extraInfo = null)
    {
        var context = new MoChainContext();
        _chainContext.Value = context;

        var node = new MoChainNode
        {
            Handler = handler,
            Operation = operation,
            StartExtraInfo = extraInfo,
            StartTime = DateTime.UtcNow
        };

        context.AddNode(node);
        return node.TraceId;
    }

    /// <summary>
    /// 检查当前是否有活跃的调用链
    /// </summary>
    /// <returns>是否有活跃的调用链</returns>
    public static bool HasActiveChain()
    {
        return _chainContext.Value != null;
    }

    /// <summary>
    /// 获取当前调用链的深度
    /// </summary>
    /// <returns>调用链深度</returns>
    public static int GetChainDepth()
    {
        var context = _chainContext.Value;
        return context?.ActiveNodes.Count ?? 0;
    }

    /// <summary>
    /// 合并远程调用链信息
    /// </summary>
    /// <param name="traceId">当前调用链节点标识</param>
    /// <param name="remoteChainInfo">远程调用链信息</param>
    public void MergeRemoteChain(string traceId, object? remoteChainInfo)
    {
        try
        {
            var context = _chainContext.Value;
            if (context == null)
            {
                logger?.LogWarning("尝试合并远程调用链但当前上下文为空: TraceId: {TraceId}", traceId);
                return;
            }

            context.MergeRemoteChain(traceId, remoteChainInfo);

            logger?.LogDebug("合并远程调用链: TraceId: {TraceId}", traceId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "合并远程调用链时发生异常: TraceId: {TraceId}", traceId);
        }
    }
}